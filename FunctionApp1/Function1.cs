using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FunctionApp1
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static void Run([CosmosDBCassandraTrigger(
            keyspaceName: "data",
            tableName: "table1",
            ContactPoint = "ContactPoint",
            User = "User",
            Password = "Password")]IReadOnlyList<Document> input, ILogger log)
        {
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Documents modified " + input.Count);
                foreach (var doc in input)
                {
                    string docstring = doc.ToString();
                    Console.Out.WriteLine(docstring);
                }
            }
        }
    }
}
