using System.Collections.Generic;
using System.Linq;
using System.Net;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Models;

namespace JsonApiDotNetCoreExample.Definitions
{
    public class PassportHooks : ResourceHooksDefinition<Passport>
    {
        public PassportHooks(IResourceGraph resourceGraph) : base(resourceGraph)
        {
        }

        public override void BeforeRead(ResourcePipeline pipeline, bool isIncluded = false, string stringId = null)
        {
            if (pipeline == ResourcePipeline.GetSingle && isIncluded)
            {
                throw new JsonApiException(new Error(HttpStatusCode.Forbidden)
                {
                    Title = "You are not allowed to include passports on individual persons."
                });
            }
        }

        public override void BeforeImplicitUpdateRelationship(IRelationshipsDictionary<Passport> resourcesByRelationship, ResourcePipeline pipeline)
        {
            resourcesByRelationship.GetByRelationship<Person>().ToList().ForEach(kvp => DoesNotTouchLockedPassports(kvp.Value));
        }

        private void DoesNotTouchLockedPassports(IEnumerable<Passport> resources)
        {
            foreach (var passport in resources ?? Enumerable.Empty<Passport>())
            {
                if (passport.IsLocked)
                {
                    throw new JsonApiException(new Error(HttpStatusCode.Forbidden)
                    {
                        Title = "You are not allowed to update fields or relationships of locked persons."
                    });
                }
            }
        }
    }
}
