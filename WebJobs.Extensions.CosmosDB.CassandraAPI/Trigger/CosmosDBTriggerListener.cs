// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Cassandra.Mapping;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Monitoring;
using Microsoft.Azure.Documents.ChangeFeedProcessor.PartitionManagement;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.CassandraAPI.Trigger;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.CassandraAPI
{
    internal class CosmosDBTriggerListener : IListener, IScaleMonitor<CosmosDBTriggerMetrics>, Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserverFactory
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly DocumentCollectionInfo _monitorCollection;
        private readonly DocumentCollectionInfo _leaseCollection;
        private readonly string _hostName;
        private readonly ChangeFeedProcessorOptions _processorOptions;
        private readonly ICosmosDBService _monitoredCosmosDBService;
        private readonly ICosmosDBService _leasesCosmosDBService;
        private readonly IHealthMonitor _healthMonitor;
        private IChangeFeedProcessor _host;
        private ChangeFeedProcessorBuilder _hostBuilder;
        private ChangeFeedProcessorBuilder _workEstimatorBuilder;
        private IRemainingWorkEstimator _workEstimator;
        private int _listenerStatus;
        private string _functionId;
        private string _keyspace;
        private string _table;
        private string _contactpoint;
        private string _user;
        private string _password;
        private ScaleMonitorDescriptor _scaleMonitorDescriptor;
        private static ISession session;

        private static readonly Dictionary<string, string> KnownDocumentClientErrors = new Dictionary<string, string>()
        {
            { "Resource Not Found", "Please check that the CosmosDB collection and leases collection exist and are listed correctly in Functions config files." },
            { "The input authorization token can't serve the request", string.Empty },
            { "The MAC signature found in the HTTP request is not the same", string.Empty },
            { "Service is currently unavailable.", string.Empty },
            { "Entity with the specified id does not exist in the system.", string.Empty },
            { "Subscription owning the database account is disabled.", string.Empty },
            { "Request rate is large", string.Empty },
            { "PartitionKey value must be supplied for this operation.", "We do not support lease collections with partitions at this time. Please create a new lease collection without partitions." },
            { "The remote name could not be resolved:", string.Empty },
            { "Owner resource does not exist", string.Empty },
            { "The specified document collection is invalid", string.Empty }
        };

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor,
            string functionId,
            string keyspace,
            string table,
            string contactpoint,
            string username,
            string password,
            //TODO: Removed below for Cassandra API binding
            //DocumentCollectionInfo documentCollectionLocation,
            //DocumentCollectionInfo leaseCollectionLocation,
            ChangeFeedProcessorOptions processorOptions,
            //TODO: Removed below for Cassandra API binding - may need to revisit in future for scale tolerance.
            //ICosmosDBService monitoredCosmosDBService,
            //ICosmosDBService leasesCosmosDBService,
            ILogger logger,
            IRemainingWorkEstimator workEstimator = null)
        {
            this._logger = logger;
            this._executor = executor;
            this._functionId = functionId;
            this._keyspace = keyspace;
            this._table = table;
            this._contactpoint = contactpoint;
            this._user = username;
            this._password = password;
            this._hostName = Guid.NewGuid().ToString();

            //TODO: Removed below for Cassandra API binding - may need to revisit in future.
            //this._monitorCollection = documentCollectionLocation;
            //this._leaseCollection = leaseCollectionLocation;

            this._processorOptions = processorOptions;

            //TODO: Removed below for Cassandra API binding - may need to revisit in future.
            //this._monitoredCosmosDBService = monitoredCosmosDBService;
            //this._leasesCosmosDBService = leasesCosmosDBService;

            this._healthMonitor = new CosmosDBTriggerHealthMonitor(this._logger);

            this._workEstimator = workEstimator;

            //TODO: Removed below for Cassandra API binding - may need to revisit in future.
            //this._scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitorCollection.DatabaseName}-{_monitorCollection.CollectionName}".ToLower());
        }

        public ScaleMonitorDescriptor Descriptor
        {
            get
            {
                return _scaleMonitorDescriptor;
            }
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this._executor);
        }

        public void Dispose()
        {
            //Nothing to dispose
        }


        public async Task StartAsync(CancellationToken cancellationToken) {

            // Connect to cassandra cluster  (Cassandra API on Azure Cosmos DB supports only TLSv1.2)
            var options = new Cassandra.SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
            options.SetHostNameResolver((ipAddress) => _contactpoint);
            Cluster cluster = Cluster.Builder().WithCredentials(_user, _password).WithPort(10350).AddContactPoint(_contactpoint).WithSSL(options).Build();

            session = cluster.Connect(_keyspace);
            IMapper mapper = new Mapper(session);

            //set initial start time for pulling the change feed
            DateTime timeBegin = DateTime.UtcNow;

            //initialise variable to store the continuation token
            byte[] pageState = null;
            _logger.LogInformation(string.Format($"reading from Cassandra API change feed..."));

            while (true)
            {
                try
                {
                    IStatement changeFeedQueryStatement = new SimpleStatement(
                    $"SELECT * FROM "+_keyspace+"."+_table+$" where COSMOS_CHANGEFEED_START_TIME() = '{timeBegin.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}'");
                    if (pageState != null)
                    {
                        changeFeedQueryStatement = changeFeedQueryStatement.SetPagingState(pageState);
                    }                   
                    RowSet rowSet = session.Execute(changeFeedQueryStatement);
                    pageState = rowSet.PagingState;
                    if (rowSet.IsFullyFetched) {
                        List<Row> rowList = rowSet.ToList();
                        CqlColumn[] columns = rowSet.Columns;
                        if (rowList.Count != 0)
                        {
                            //convert that Cassandra formatted resultset to Document in order to keep same interface contract
                            List<Document> docs = new List<Document>();
                            for (int i = 0; i < rowList.Count; i++)
                            {
                                Document doc = new Document();
                                foreach (CqlColumn col in columns)
                                {
                                    doc.SetPropertyValue(col.Name, rowList[i].GetValue<dynamic>(col.Name));                                   
                                }
                                docs.Add(doc);
                            }
                            //TODO: this is executed from ChangeFeed Observer in original SQL API implementation - may need to revisit
                            await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, cancellationToken);
                        }
                    }                    
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception " + e);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this._host != null)
                {
                    await this._host.StopAsync().ConfigureAwait(false);
                    this._listenerStatus = ListenerNotRegistered;
                }
            }
            catch (Exception ex)
            {
                //TODO: stopping may not work in exactly the same way as SQL API
                this._logger.LogWarning($"Stopping the observer failed, potentially it was never started. Exception: {ex.Message}.");
            }
        }

        internal virtual async Task StartProcessorAsync()
        {
            if (this._host == null)
            {
                this._host = await this._hostBuilder.BuildAsync().ConfigureAwait(false);
            }

            await this._host.StartAsync().ConfigureAwait(false);
        }

        private void InitializeBuilder()
        {
            //TODO: the current processing in StartAsync needs to be integrated into a change feed 
            //processor, per similar approach below for SQL API, in order to be scale tolerant. Below code is redundant.
            if (this._hostBuilder == null)
            {
                this._hostBuilder = new ChangeFeedProcessorBuilder()
                    .WithHostName(this._hostName)
                    .WithFeedDocumentClient(this._monitoredCosmosDBService.GetClient())
                    .WithLeaseDocumentClient(this._leasesCosmosDBService.GetClient())
                    .WithFeedCollection(this._monitorCollection)
                    .WithLeaseCollection(this._leaseCollection)
                    .WithProcessorOptions(this._processorOptions)
                    .WithHealthMonitor(this._healthMonitor)
                    .WithObserverFactory(this);
            }
        }

        private async Task<IRemainingWorkEstimator> GetWorkEstimatorAsync()
        {
            if (_workEstimatorBuilder == null)
            {
                _workEstimatorBuilder = new ChangeFeedProcessorBuilder()
                    .WithHostName(this._hostName)
                    .WithFeedDocumentClient(this._monitoredCosmosDBService.GetClient())
                    .WithLeaseDocumentClient(this._leasesCosmosDBService.GetClient())
                    .WithFeedCollection(this._monitorCollection)
                    .WithLeaseCollection(this._leaseCollection)
                    .WithProcessorOptions(this._processorOptions)
                    .WithHealthMonitor(this._healthMonitor)
                    .WithObserverFactory(this);
            }

            if (_workEstimator == null)
            {
                _workEstimator = await _workEstimatorBuilder.BuildEstimatorAsync();
            }

            return _workEstimator;
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await GetMetricsAsync();
        }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            int partitionCount = 0;
            long remainingWork = 0;
            IReadOnlyList<RemainingPartitionWork> partitionWorkList = null;

            try
            {
                IRemainingWorkEstimator workEstimator = await GetWorkEstimatorAsync();
                partitionWorkList = await workEstimator.GetEstimatedRemainingWorkPerPartitionAsync();

                partitionCount = partitionWorkList.Count;
                remainingWork = partitionWorkList.Sum(item => item.RemainingWork);
            }
            catch (Exception e) when (e is DocumentClientException || e is InvalidOperationException)
            {
                if (!TryHandleDocumentClientException(e))
                {
                    _logger.LogWarning("Unable to handle {0}: {1}", e.GetType().ToString(), e.Message);
                    if (e is InvalidOperationException)
                    {
                        throw;
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                string errormsg;

                var webException = e.InnerException as WebException;
                if (webException != null &&
                    webException.Status == WebExceptionStatus.ProtocolError)
                {
                    string statusCode = ((HttpWebResponse)webException.Response).StatusCode.ToString();
                    string statusDesc = ((HttpWebResponse)webException.Response).StatusDescription;
                    errormsg = string.Format("CosmosDBTrigger status {0}: {1}.", statusCode, statusDesc);
                }
                else if (webException != null &&
                    webException.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    errormsg = string.Format("CosmosDBTrigger Exception message: {0}.", webException.Message);
                }
                else
                {
                    errormsg = e.ToString();
                }

                _logger.LogWarning(errormsg);
            }

            return new CosmosDBTriggerMetrics
            {
                Timestamp = DateTime.UtcNow,
                PartitionCount = partitionCount,
                RemainingWork = remainingWork
            };
        }

        //TODO: Below not leveraged for Cassandra API binding - need to revisit in future.
        ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.ToArray());
        }

        private ScaleStatus GetScaleStatusCore(int workerCount, CosmosDBTriggerMetrics[] metrics)
        {
            ScaleStatus status = new ScaleStatus
            {
                Vote = ScaleVote.None
            };

            const int NumberOfSamplesToConsider = 5;

            // Unable to determine the correct vote with no metrics.
            if (metrics == null)
            {
                return status;
            }

            // We shouldn't assign more workers than there are partitions (Cosmos DB, Event Hub, Service Bus Queue/Topic)
            // This check is first, because it is independent of load or number of samples.
            int partitionCount = metrics.Length > 0 ? metrics.Last().PartitionCount : 0;
            if (partitionCount > 0 && partitionCount < workerCount)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(string.Format($"WorkerCount ({workerCount}) > PartitionCount ({partitionCount})."));
                _logger.LogInformation(string.Format($"Number of instances ({workerCount}) is too high relative to number " +
                                                     $"of partitions for collection ({this._monitorCollection.CollectionName}, {partitionCount})."));
                return status;
            }

            // At least 5 samples are required to make a scale decision for the rest of the checks.
            if (metrics.Length < NumberOfSamplesToConsider)
            {
                return status;
            }

            // Maintain a minimum ratio of 1 worker per 1,000 items of remaining work.
            long latestRemainingWork = metrics.Last().RemainingWork;
            if (latestRemainingWork > workerCount * 1000)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(string.Format($"RemainingWork ({latestRemainingWork}) > WorkerCount ({workerCount}) * 1,000."));
                _logger.LogInformation(string.Format($"Remaining work for collection ({this._monitorCollection.CollectionName}, {latestRemainingWork}) " +
                                                     $"is too high relative to the number of instances ({workerCount})."));
                return status;
            }

            bool documentsWaiting = metrics.All(m => m.RemainingWork > 0);
            if (documentsWaiting && partitionCount > 0 && partitionCount > workerCount)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(string.Format($"CosmosDB collection '{this._monitorCollection.CollectionName}' has documents waiting to be processed."));
                _logger.LogInformation(string.Format($"There are {workerCount} instances relative to {partitionCount} partitions."));
                return status;
            }

            // Check to see if the trigger source has been empty for a while. Only if all trigger sources are empty do we scale down.
            bool isIdle = metrics.All(m => m.RemainingWork == 0);
            if (isIdle)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(string.Format($"'{this._monitorCollection.CollectionName}' is idle."));
                return status;
            }

            // Samples are in chronological order. Check for a continuous increase in work remaining.
            // If detected, this results in an automatic scale out for the site container.
            bool remainingWorkIncreasing =
                IsTrueForLast(
                    metrics,
                    NumberOfSamplesToConsider,
                    (prev, next) => prev.RemainingWork < next.RemainingWork) && metrics[0].RemainingWork > 0;
            if (remainingWorkIncreasing)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation($"Remaining work is increasing for '{this._monitorCollection.CollectionName}'.");
                return status;
            }

            bool remainingWorkDecreasing =
                IsTrueForLast(
                    metrics,
                    NumberOfSamplesToConsider,
                    (prev, next) => prev.RemainingWork > next.RemainingWork);
            if (remainingWorkDecreasing)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation($"Remaining work is decreasing for '{this._monitorCollection.CollectionName}'.");
                return status;
            }

            _logger.LogInformation($"CosmosDB collection '{this._monitorCollection.CollectionName}' is steady.");

            return status;
        }

        // Since all exceptions in the Document client are thrown as DocumentClientExceptions, we have to parse their error strings because we dont have access to the internal types
        // In the form Microsoft.Azure.Documents.DocumentClientException or Microsoft.Azure.Documents.UnauthorizedException
        private bool TryHandleDocumentClientException(Exception exception)
        {
            string errormsg = null;
            string exceptionMessage = exception.Message;

            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                foreach (KeyValuePair<string, string> exceptionString in KnownDocumentClientErrors)
                {
                    if (exceptionMessage.IndexOf(exceptionString.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        errormsg = !string.IsNullOrEmpty(exceptionString.Value) ? exceptionString.Value : exceptionMessage;
                    }
                }
            }

            if (!string.IsNullOrEmpty(errormsg))
            {
                _logger.LogWarning(errormsg);
                return true;
            }

            return false;
        }

        private static bool IsTrueForLast(IList<CosmosDBTriggerMetrics> metrics, int count, Func<CosmosDBTriggerMetrics, CosmosDBTriggerMetrics, bool> predicate)
        {
            Debug.Assert(count > 1, "count must be greater than 1.");
            Debug.Assert(count <= metrics.Count, "count must be less than or equal to the list size.");

            // Walks through the list from left to right starting at len(samples) - count.
            for (int i = metrics.Count - count; i < metrics.Count - 1; i++)
            {
                if (!predicate(metrics[i], metrics[i + 1]))
                {
                    return false;
                }
            }

            return true;
        }
        public static bool ValidateServerCertificate(
    object sender,
    X509Certificate certificate,
    X509Chain chain,
    SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }
    }
}
