package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.cosmosdbcassandra.annotation.CosmosDBCassandraTrigger;
import com.microsoft.azure.functions.*;

public class Function {
    @FunctionName("cosmosDbProcessor")
    public void cosmosDbProcessor(
            @CosmosDBCassandraTrigger(name = "input", keyspaceName = "data", tableName = "table1", ContactPoint = "ContactPoint", FeedPollDelay = 5000, User = "User", Password = "Password") String[] items,
            final ExecutionContext context) {
        for (String string : items) {
            // System.out.println(string);
            context.getLogger().info("doc: " + string);
        }
        context.getLogger().info(items.length + "item(s) is/are changed.");
    }
}