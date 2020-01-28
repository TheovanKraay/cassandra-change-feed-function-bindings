// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

//using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    //internal class CosmosDBTriggerObserver : IChangeFeedObserver
    internal class CosmosDBTriggerObserver
    {
        private readonly ITriggeredFunctionExecutor executor;

        public CosmosDBTriggerObserver(ITriggeredFunctionExecutor executor)
        {
            this.executor = executor;
        }

        //public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        //{
        //    if (context == null)
        //    {
        //        throw new ArgumentNullException("context", "Missing observer context");
        //    }
        //    return Task.CompletedTask;
        //}

        //public Task OpenAsync(IChangeFeedObserverContext context)
        //{
        //    if (context == null)
        //    {
        //        throw new ArgumentNullException("context", "Missing observer context");
        //    }
        //    return Task.CompletedTask;
        //}

        //public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Document> docs, CancellationToken cancellationToken)
        //{
        //    Console.Out.WriteLine("in ProcessChangesAsync Try/Catch......");
        //    return this.executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, cancellationToken);
        //}
    }
}
