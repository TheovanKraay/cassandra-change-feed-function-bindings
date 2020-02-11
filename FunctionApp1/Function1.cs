using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
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
            FeedPollDelay = 5000,
            User = "User",
            Password = "Password")]IReadOnlyList<JArray> input)
        {
            if (input != null)
            {
                if (input.Count != 0)
                {
                    for (int i = 0; i < input.Count; i++)
                    {
                        Console.WriteLine("Cassandra row: " +input[i].ToString());
                    }
                }
            }
        }
    }
}
