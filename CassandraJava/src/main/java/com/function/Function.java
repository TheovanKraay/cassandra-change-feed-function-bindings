package com.function;

import java.util.*;
import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

public class Function {
    @FunctionName("cosmosDbProcessor")
    public void cosmosDbProcessor(
                @CassandraBindingTrigger(name = "input") String[] items, 
				final ExecutionContext context) 
                {
                for (String string : items) {
                    //System.out.println(string);
                    context.getLogger().info("doc: "+string);
                }
        context.getLogger().info(items.length + "item(s) is/are changed.");
    }
}