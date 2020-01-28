// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines the [CosmosDBTrigger] attribute
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class CosmosDBCassandraTriggerAttribute : Attribute
    {
        /// <summary>
        /// Triggers an event when changes occur on a monitored collection
        /// </summary>
        /// <param name="databaseName">Name of the database of the collection to monitor for changes</param>
        /// <param name="collectionName">Name of the collection to monitor for changes</param>
        public CosmosDBCassandraTriggerAttribute(string keyspaceName, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Missing information for the collection to monitor", "tableName");
            }

            if (string.IsNullOrWhiteSpace(keyspaceName))
            {
                throw new ArgumentException("Missing information for the collection to monitor", "KeyspaceName");
            }

            KeyspaceName = keyspaceName;
            TableName = tableName;
        }
        
        /// <summary>
        /// Name of the collection to monitor for changes
        /// </summary>
        [AppSetting]
        public string ContactPoint { get; set; }


        /// <summary>
        /// Name of the collection to monitor for changes
        /// </summary>
        [AppSetting]
        public string User { get; set; }


        /// <summary>
        /// Name of the collection to monitor for changes
        /// </summary>
        [AppSetting]
        public string Password { get; set; }


        /// <summary>
        /// Name of the collection to monitor for changes
        /// </summary>
        public string KeyspaceName { get; private set; }

        /// <summary>
        /// Name of the database containing the collection to monitor for changes
        /// </summary>
        public string TableName { get; private set; }
    }
}
