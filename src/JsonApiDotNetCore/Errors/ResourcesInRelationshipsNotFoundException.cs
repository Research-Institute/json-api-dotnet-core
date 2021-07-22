using System.Collections.Generic;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Errors
{
    /// <summary>
    /// The error that is thrown when referencing one or more non-existing resources in one or more relationships.
    /// </summary>
    [PublicAPI]
    public sealed class ResourcesInRelationshipsNotFoundException : JsonApiException
    {
        public ResourcesInRelationshipsNotFoundException(IEnumerable<MissingResourceInRelationship> missingResources)
            : base(missingResources.Select(CreateError))
        {
        }

        private static Error CreateError(MissingResourceInRelationship missingResourceInRelationship)
        {
            return new(HttpStatusCode.NotFound)
            {
                Title = "A related resource does not exist.",
                Detail = $"Related resource of type '{missingResourceInRelationship.ResourceType}' with ID '{missingResourceInRelationship.ResourceId}' " +
                    $"in relationship '{missingResourceInRelationship.RelationshipName}' does not exist."
            };
        }
    }
}
