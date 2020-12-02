using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    /// <summary>
    /// The base class to derive resource-specific controllers from.
    /// This class delegates all work to <see cref="BaseJsonApiController{TResource, TId}"/> but adds attributes for routing templates.
    /// If you want to provide routing templates yourself, you should derive from BaseJsonApiController directly.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <typeparam name="TId">The resource identifier type.</typeparam>
    public class JsonApiController<TResource, TId> : BaseJsonApiController<TResource, TId> where TResource : class, IIdentifiable<TId>
    {
        /// <inheritdoc />
        public JsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<TResource, TId> resourceService)
            : base(options, loggerFactory, resourceService)
        { }

        /// <inheritdoc />
        [HttpGet]
        public override async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            return await base.GetAsync(cancellationToken);
        }

        /// <inheritdoc />
        [HttpGet("{id}")]
        public override async Task<IActionResult> GetAsync(TId id, CancellationToken cancellationToken)
        {
            return await base.GetAsync(id, cancellationToken);
        }

        /// <inheritdoc />
        [HttpGet("{id}/{relationshipName}")]
        public override async Task<IActionResult> GetSecondaryAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            return await base.GetSecondaryAsync(id, relationshipName, cancellationToken);
        }

        /// <inheritdoc />
        [HttpGet("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> GetRelationshipAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            return await base.GetRelationshipAsync(id, relationshipName, cancellationToken);
        }

        /// <inheritdoc />
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] TResource resource, CancellationToken cancellationToken)
        {
            return await base.PostAsync(resource, cancellationToken);
        }

        /// <inheritdoc />
        [HttpPost("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> PostRelationshipAsync(
            TId id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds, CancellationToken cancellationToken)
        {
            return await base.PostRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);
        }

        /// <inheritdoc />
        [HttpPatch("{id}")]
        public override async Task<IActionResult> PatchAsync(TId id, [FromBody] TResource resource, CancellationToken cancellationToken)
        {
            return await base.PatchAsync(id, resource, cancellationToken);
        }

        /// <inheritdoc />
        [HttpPatch("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> PatchRelationshipAsync(
            TId id, string relationshipName, [FromBody] object secondaryResourceIds, CancellationToken cancellationToken)
        {
            return await base.PatchRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);
        }

        /// <inheritdoc />
        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(TId id, CancellationToken cancellationToken)
        {
            return await base.DeleteAsync(id, cancellationToken);
        }

        /// <inheritdoc />
        [HttpDelete("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> DeleteRelationshipAsync(TId id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds, CancellationToken cancellationToken)
        {
            return await base.DeleteRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);
        }
    }

    /// <inheritdoc />
    public class JsonApiController<TResource> : JsonApiController<TResource, int> where TResource : class, IIdentifiable<int>
    {
        /// <inheritdoc />
        public JsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<TResource, int> resourceService)
            : base(options, loggerFactory, resourceService)
        { }
    }
}
