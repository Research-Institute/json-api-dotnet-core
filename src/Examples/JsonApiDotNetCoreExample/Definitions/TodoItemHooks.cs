using System.Collections.Generic;
using System.Linq;
using System.Net;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Models;

namespace JsonApiDotNetCoreExample.Definitions
{
    public class TodoItemHooks : LockableHooksDefinition<TodoItem>
    {
        public TodoItemHooks(IResourceGraph resourceGraph) : base(resourceGraph) { }

        public override void BeforeRead(ResourcePipeline pipeline, bool isIncluded = false, string stringId = null)
        {
            if (stringId == "1337")
            {
                throw new JsonApiException(new Error(HttpStatusCode.Forbidden)
                {
                    Title = "You are not allowed to update the author of todo items."
                });
            }
        }

        public override void BeforeImplicitUpdateRelationship(IRelationshipsDictionary<TodoItem> resourcesByRelationship, ResourcePipeline pipeline)
        {
            List<TodoItem> todos = resourcesByRelationship.GetByRelationship<Person>().SelectMany(kvp => kvp.Value).ToList();
            DisallowLocked(todos);
        }
    }
}
