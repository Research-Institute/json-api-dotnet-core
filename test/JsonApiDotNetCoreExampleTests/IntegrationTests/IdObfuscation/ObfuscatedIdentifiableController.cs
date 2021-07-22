using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    public abstract class ObfuscatedIdentifiableController<TResource> : BaseJsonApiController<TResource>
        where TResource : class, IIdentifiable<int>
    {
        private readonly HexadecimalCodec _codec = new();

        protected ObfuscatedIdentifiableController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceService<TResource> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }

        [HttpGet]
        public override Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            return base.GetAsync(cancellationToken);
        }

        [HttpGet("{id}")]
        public Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.GetAsync(idValue, cancellationToken);
        }

        [HttpGet("{id}/{relationshipName}")]
        public Task<IActionResult> GetSecondaryAsync(string id, string relationshipName, CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.GetSecondaryAsync(idValue, relationshipName, cancellationToken);
        }

        [HttpGet("{id}/relationships/{relationshipName}")]
        public Task<IActionResult> GetRelationshipAsync(string id, string relationshipName, CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.GetRelationshipAsync(idValue, relationshipName, cancellationToken);
        }

        [HttpPost]
        public override Task<IActionResult> PostAsync([FromBody] TResource resource, CancellationToken cancellationToken)
        {
            return base.PostAsync(resource, cancellationToken);
        }

        [HttpPost("{id}/relationships/{relationshipName}")]
        public Task<IActionResult> PostRelationshipAsync(string id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.PostRelationshipAsync(idValue, relationshipName, secondaryResourceIds, cancellationToken);
        }

        [HttpPatch("{id}")]
        public Task<IActionResult> PatchAsync(string id, [FromBody] TResource resource, CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.PatchAsync(idValue, resource, cancellationToken);
        }

        [HttpPatch("{id}/relationships/{relationshipName}")]
        public Task<IActionResult> PatchRelationshipAsync(string id, string relationshipName, [FromBody] object secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.PatchRelationshipAsync(idValue, relationshipName, secondaryResourceIds, cancellationToken);
        }

        [HttpDelete("{id}")]
        public Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.DeleteAsync(idValue, cancellationToken);
        }

        [HttpDelete("{id}/relationships/{relationshipName}")]
        public Task<IActionResult> DeleteRelationshipAsync(string id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            int idValue = _codec.Decode(id);
            return base.DeleteRelationshipAsync(idValue, relationshipName, secondaryResourceIds, cancellationToken);
        }
    }
}
