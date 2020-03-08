/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.microsoft.azure.functions.cosmosdbcassandra.annotation;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;
import com.microsoft.azure.functions.annotation.CustomBinding;

/**
 * <p>
 * Place this on a parameter whose value would come from CosmosDB, and causing the method to run
 * when CosmosDB data is changed. The parameter type can be one of the following:
 * </p>
 *
 * <ul>
 * <li>Some native Java types such as String</li>
 * <li>Nullable values using Optional&lt;T&gt;</li>
 * <li>Any POJO type</li>
 * </ul>
 *
 * <p>
 * The following example shows a Java function that is invoked when there are inserts or updates in
 * the specified Cosmos DB Cassandra API Keyspace and table.
 * </p>
 *
 * <pre>
 *   @FunctionName("cosmosDbProcessor")
 *   public void cosmosDbProcessor(
 *           @CosmosDBCassandraTrigger(name = "input", keyspaceName = "data", tableName = "table1", ContactPoint = "ContactPoint", FeedPollDelay = 5000, User = "User", Password = "Password") String[] items,
 *           final ExecutionContext context) {
 *       for (String string : items) {
 *           context.getLogger().info("doc: " + string);
 *       }
 *       context.getLogger().info(items.length + "item(s) is/are changed.");
 *   }
 * </pre>
 *
 * @since 1.0.0
 */
@Retention(RetentionPolicy.RUNTIME)
@Target({ ElementType.PARAMETER })
@CustomBinding(direction = "in", name = "input", type = "CosmosDBCassandraTrigger")
public @interface CosmosDBCassandraTrigger {
  /**
   * The variable name used in function.json.
   * 
   * @return The variable name used in function.json.
   */
  String name();

  /**
   * <p>
   * Defines how Functions runtime should treat the parameter value. Possible values are:
   * </p>
   * <ul>
   * <li>"": get the value as a string, and try to deserialize to actual parameter type like
   * POJO</li>
   * <li>string: always get the value as a string</li>
   * <li>binary: get the value as a binary data, and try to deserialize to actual parameter type
   * byte[]</li>
   * </ul>
   * 
   * @return The dataType which will be used by the Functions runtime.
   */
  String dataType() default "";

  /**
   * Defines the Keyspace name of the CosmosDB Cassandra Keyspace to which to bind.
   * 
   * @return The Keyspace name string.
   */
  String keyspaceName();

  /**
   * Defines the table name of the CosmosDB Cassandra table to which to bind.
   * 
   * @return The table string.
   */
  String tableName();

  /**
   * Defines Cassandra contact point (host)
   */
  String ContactPoint();

  /**
   * Defines the Cassandra User ID
   */
  String User();

  /**
   * Defines Cassandra password (primary key)
   */
  String Password();


  /**
   * Customizes the delay in milliseconds in between polling a partition for new changes on the
   * feed, after all current changes are drained. Default is 5000 (5 seconds).
   * 
   * @return feedPollDelay
   */
  int FeedPollDelay() default 5000;

  /**
   * Gets or sets whether change feed in the Azure Cosmos DB service should start from beginning
   * (true) or from current (false). By default it's start from current (false).
   * 
   * @return Configuration whether change feed should start from beginning
   */
  boolean StartFromBeginning() default false;

}