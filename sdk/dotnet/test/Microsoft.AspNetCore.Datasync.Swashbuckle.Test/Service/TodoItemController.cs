﻿// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Datasync.EFCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Datasync.Swashbuckle.Test.Service
{
    [Route("tables/[controller]")]
    [ExcludeFromCodeCoverage]
    public class TodoItemController : TableController<TodoItem>
    {
        public TodoItemController(ServiceDbContext context, ILogger<TodoItem> logger) : base()
        {
            Repository = new EntityTableRepository<TodoItem>(context);
            Logger = logger;
        }
    }
}
