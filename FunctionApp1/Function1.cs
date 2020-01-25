using Cassandra;
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
            FeedPollDelay = 500,
            User = "User",
            Password = "Password")]IReadOnlyList<Row> input, ILogger log)
        {
            if (input != null)
            {
                if (input.Count != 0)
                {
                    for (int i = 0; i < input.Count; i++)
                    {
                        string name = input[i].GetValue<string>("name");
                        string email = input[i].GetValue<string>("email");
                        Console.WriteLine("name: " + name); 
                        Console.WriteLine("email: " + email);
                    }
                }
            }
        }
    }
}
