using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    /// <summary>
    /// Implements the foundational ASP.NET Core controller layer in the JsonApiDotNetCore architecture that delegates to a Resource Service.
    /// </summary>
    /// <typeparam name="TResource">
    /// The resource type.
    /// </typeparam>
    /// <typeparam name="TId">
    /// The resource identifier type.
    /// </typeparam>
    public abstract class BaseJsonApiController<TResource, TId> : CoreJsonApiController
        where TResource : class, IIdentifiable<TId>
    {
        private readonly IJsonApiOptions _options;
        private readonly IGetAllService<TResource, TId> _getAll;
        private readonly IGetByIdService<TResource, TId> _getById;
        private readonly IGetSecondaryService<TResource, TId> _getSecondary;
        private readonly IGetRelationshipService<TResource, TId> _getRelationship;
        private readonly ICreateService<TResource, TId> _create;
        private readonly IAddToRelationshipService<TResource, TId> _addToRelationship;
        private readonly IUpdateService<TResource, TId> _update;
        private readonly ISetRelationshipService<TResource, TId> _setRelationship;
        private readonly IDeleteService<TResource, TId> _delete;
        private readonly IRemoveFromRelationshipService<TResource, TId> _removeFromRelationship;
        private readonly TraceLogWriter<BaseJsonApiController<TResource, TId>> _traceWriter;

        /// <summary>
        /// Creates an instance from a read/write service.
        /// </summary>
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceService<TResource, TId> resourceService)
            : this(options, loggerFactory, resourceService, resourceService)
        {
        }

        /// <summary>
        /// Creates an instance from separate services for reading and writing.
        /// </summary>
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceQueryService<TResource, TId> queryService = null,
            IResourceCommandService<TResource, TId> commandService = null)
            : this(options, loggerFactory, queryService, queryService, queryService, queryService, commandService, commandService, commandService,
                commandService, commandService, commandService)
        {
        }

        /// <summary>
        /// Creates an instance from separate services for the various individual read and write methods.
        /// </summary>
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IGetAllService<TResource, TId> getAll = null,
            IGetByIdService<TResource, TId> getById = null, IGetSecondaryService<TResource, TId> getSecondary = null,
            IGetRelationshipService<TResource, TId> getRelationship = null, ICreateService<TResource, TId> create = null,
            IAddToRelationshipService<TResource, TId> addToRelationship = null, IUpdateService<TResource, TId> update = null,
            ISetRelationshipService<TResource, TId> setRelationship = null, IDeleteService<TResource, TId> delete = null,
            IRemoveFromRelationshipService<TResource, TId> removeFromRelationship = null)
        {
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(loggerFactory, nameof(loggerFactory));

            _options = options;
            _traceWriter = new TraceLogWriter<BaseJsonApiController<TResource, TId>>(loggerFactory);
            _getAll = getAll;
            _getById = getById;
            _getSecondary = getSecondary;
            _getRelationship = getRelationship;
            _create = create;
            _addToRelationship = addToRelationship;
            _update = update;
            _setRelationship = setRelationship;
            _delete = delete;
            _removeFromRelationship = removeFromRelationship;
        }

        /// <summary>
        /// Gets a collection of top-level (non-nested) resources. Example: GET /articles HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart();

            if (_getAll == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Get);
            }

            IReadOnlyCollection<TResource> resources = await _getAll.GetAsync(cancellationToken);

            return Ok(resources);
        }

        /// <summary>
        /// Gets a single top-level (non-nested) resource by ID. Example: /articles/1
        /// </summary>
        public virtual async Task<IActionResult> GetAsync(TId id, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id
            });

            if (_getById == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Get);
            }

            TResource resource = await _getById.GetAsync(id, cancellationToken);

            return Ok(resource);
        }

        /// <summary>
        /// Gets a single resource or multiple resources at a nested endpoint. Examples: GET /articles/1/author HTTP/1.1 GET /articles/1/revisions HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetSecondaryAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));

            if (_getSecondary == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Get);
            }

            object relationship = await _getSecondary.GetSecondaryAsync(id, relationshipName, cancellationToken);

            return Ok(relationship);
        }

        /// <summary>
        /// Gets a single resource relationship. Example: GET /articles/1/relationships/author HTTP/1.1 Example: GET /articles/1/relationships/revisions HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetRelationshipAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));

            if (_getRelationship == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Get);
            }

            object rightResources = await _getRelationship.GetRelationshipAsync(id, relationshipName, cancellationToken);

            return Ok(rightResources);
        }

        /// <summary>
        /// Creates a new resource with attributes, relationships or both. Example: POST /articles HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> PostAsync([FromBody] TResource resource, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                resource
            });

            ArgumentGuard.NotNull(resource, nameof(resource));

            if (_create == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Post);
            }

            if (!_options.AllowClientGeneratedIds && resource.StringId != null)
            {
                throw new ResourceIdInCreateResourceNotAllowedException();
            }

            if (_options.ValidateModelState && !ModelState.IsValid)
            {
                throw new InvalidModelStateException(ModelState, typeof(TResource), _options.IncludeExceptionStackTraceInErrors,
                    _options.SerializerNamingStrategy);
            }

            TResource newResource = await _create.CreateAsync(resource, cancellationToken);

            string resourceId = (newResource ?? resource).StringId;
            string locationUrl = $"{HttpContext.Request.Path}/{resourceId}";

            if (newResource == null)
            {
                HttpContext.Response.Headers["Location"] = locationUrl;
                return NoContent();
            }

            return Created(locationUrl, newResource);
        }

        /// <summary>
        /// Adds resources to a to-many relationship. Example: POST /articles/1/revisions HTTP/1.1
        /// </summary>
        /// <param name="id">
        /// The identifier of the primary resource.
        /// </param>
        /// <param name="relationshipName">
        /// The relationship to add resources to.
        /// </param>
        /// <param name="secondaryResourceIds">
        /// The set of resources to add to the relationship.
        /// </param>
        /// <param name="cancellationToken">
        /// Propagates notification that request handling should be canceled.
        /// </param>
        public virtual async Task<IActionResult> PostRelationshipAsync(TId id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));
            ArgumentGuard.NotNull(secondaryResourceIds, nameof(secondaryResourceIds));

            if (_addToRelationship == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Post);
            }

            await _addToRelationship.AddToToManyRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Updates the attributes and/or relationships of an existing resource. Only the values of sent attributes are replaced. And only the values of sent
        /// relationships are replaced. Example: PATCH /articles/1 HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> PatchAsync(TId id, [FromBody] TResource resource, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                resource
            });

            ArgumentGuard.NotNull(resource, nameof(resource));

            if (_update == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            }

            if (_options.ValidateModelState && !ModelState.IsValid)
            {
                throw new InvalidModelStateException(ModelState, typeof(TResource), _options.IncludeExceptionStackTraceInErrors,
                    _options.SerializerNamingStrategy);
            }

            TResource updated = await _update.UpdateAsync(id, resource, cancellationToken);
            return updated == null ? NoContent() : Ok(updated);
        }

        /// <summary>
        /// Performs a complete replacement of a relationship on an existing resource. Example: PATCH /articles/1/relationships/author HTTP/1.1 Example: PATCH
        /// /articles/1/relationships/revisions HTTP/1.1
        /// </summary>
        /// <param name="id">
        /// The identifier of the primary resource.
        /// </param>
        /// <param name="relationshipName">
        /// The relationship for which to perform a complete replacement.
        /// </param>
        /// <param name="secondaryResourceIds">
        /// The resource or set of resources to assign to the relationship.
        /// </param>
        /// <param name="cancellationToken">
        /// Propagates notification that request handling should be canceled.
        /// </param>
        public virtual async Task<IActionResult> PatchRelationshipAsync(TId id, string relationshipName, [FromBody] object secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));

            if (_setRelationship == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            }

            await _setRelationship.SetRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Deletes an existing resource. Example: DELETE /articles/1 HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> DeleteAsync(TId id, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id
            });

            if (_delete == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Delete);
            }

            await _delete.DeleteAsync(id, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Removes resources from a to-many relationship. Example: DELETE /articles/1/relationships/revisions HTTP/1.1
        /// </summary>
        /// <param name="id">
        /// The identifier of the primary resource.
        /// </param>
        /// <param name="relationshipName">
        /// The relationship to remove resources from.
        /// </param>
        /// <param name="secondaryResourceIds">
        /// The set of resources to remove from the relationship.
        /// </param>
        /// <param name="cancellationToken">
        /// Propagates notification that request handling should be canceled.
        /// </param>
        public virtual async Task<IActionResult> DeleteRelationshipAsync(TId id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));
            ArgumentGuard.NotNull(secondaryResourceIds, nameof(secondaryResourceIds));

            if (_removeFromRelationship == null)
            {
                throw new RequestMethodNotAllowedException(HttpMethod.Delete);
            }

            await _removeFromRelationship.RemoveFromToManyRelationshipAsync(id, relationshipName, secondaryResourceIds, cancellationToken);

            return NoContent();
        }
    }

    /// <inheritdoc />
    public abstract class BaseJsonApiController<TResource> : BaseJsonApiController<TResource, int>
        where TResource : class, IIdentifiable<int>
    {
        /// <inheritdoc />
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceService<TResource, int> resourceService)
            : base(options, loggerFactory, resourceService, resourceService)
        {
        }

        /// <inheritdoc />
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceQueryService<TResource, int> queryService = null,
            IResourceCommandService<TResource, int> commandService = null)
            : base(options, loggerFactory, queryService, commandService)
        {
        }

        /// <inheritdoc />
        protected BaseJsonApiController(IJsonApiOptions options, ILoggerFactory loggerFactory, IGetAllService<TResource, int> getAll = null,
            IGetByIdService<TResource, int> getById = null, IGetSecondaryService<TResource, int> getSecondary = null,
            IGetRelationshipService<TResource, int> getRelationship = null, ICreateService<TResource, int> create = null,
            IAddToRelationshipService<TResource, int> addToRelationship = null, IUpdateService<TResource, int> update = null,
            ISetRelationshipService<TResource, int> setRelationship = null, IDeleteService<TResource, int> delete = null,
            IRemoveFromRelationshipService<TResource, int> removeFromRelationship = null)
            : base(options, loggerFactory, getAll, getById, getSecondary, getRelationship, create, addToRelationship, update, setRelationship, delete,
                removeFromRelationship)
        {
        }
    }
}
