// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Bindings
{
    internal class CosmosDBJArrayBuilder : IAsyncConverter<CosmosDBAttribute, JArray>
    {
        private CosmosDBEnumerableBuilder<JObject> _builder;

        public CosmosDBJArrayBuilder(CosmosDBExtensionConfigProvider configProvider)
        {
            _builder = new CosmosDBEnumerableBuilder<JObject>(configProvider);
        }

        public async Task<JArray> ConvertAsync(CosmosDBAttribute attribute, CancellationToken cancellationToken)
        {
            IEnumerable<JObject> results = await _builder.ConvertAsync(attribute, cancellationToken);
            return JArray.FromObject(results);
        }
    }
}
