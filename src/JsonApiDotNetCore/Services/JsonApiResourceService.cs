using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Hooks.Internal;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Services
{
    /// <inheritdoc />
    public class JsonApiResourceService<TResource, TId> :
        IResourceService<TResource, TId>
        where TResource : class, IIdentifiable<TId>
    {
        private readonly IResourceRepositoryAccessor _repositoryAccessor;
        private readonly IQueryLayerComposer _queryLayerComposer;
        private readonly IPaginationContext _paginationContext;
        private readonly IJsonApiOptions _options;
        private readonly TraceLogWriter<JsonApiResourceService<TResource, TId>> _traceWriter;
        private readonly IJsonApiRequest _request;
        private readonly IResourceChangeTracker<TResource> _resourceChangeTracker;
        private readonly IResourceFactory _resourceFactory;
        private readonly IResourceHookExecutorFacade _hookExecutor;

        public JsonApiResourceService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceFactory resourceFactory,
            IResourceHookExecutorFacade hookExecutor)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _repositoryAccessor = repositoryAccessor ?? throw new ArgumentNullException(nameof(repositoryAccessor));
            _queryLayerComposer = queryLayerComposer ?? throw new ArgumentNullException(nameof(queryLayerComposer));
            _paginationContext = paginationContext ?? throw new ArgumentNullException(nameof(paginationContext));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _traceWriter = new TraceLogWriter<JsonApiResourceService<TResource, TId>>(loggerFactory);
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _resourceChangeTracker = resourceChangeTracker ?? throw new ArgumentNullException(nameof(resourceChangeTracker));
            _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
            _hookExecutor = hookExecutor ?? throw new ArgumentNullException(nameof(hookExecutor));
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyCollection<TResource>> GetAsync()
        {
            _traceWriter.LogMethodStart();

            _hookExecutor.BeforeReadMany<TResource>();

            if (_options.IncludeTotalResourceCount)
            {
                var topFilter = _queryLayerComposer.GetTopFilterFromConstraints();
                _paginationContext.TotalResourceCount = await _repositoryAccessor.CountAsync(typeof(TResource), topFilter);

                if (_paginationContext.TotalResourceCount == 0)
                {
                    return Array.Empty<TResource>();
                }
            }

            var queryLayer = _queryLayerComposer.ComposeFromConstraints(_request.PrimaryResource);
            var resources = await _repositoryAccessor.GetAsync<TResource>(queryLayer);

            if (queryLayer.Pagination?.PageSize != null && queryLayer.Pagination.PageSize.Value == resources.Count)
            {
                _paginationContext.IsPageFull = true;
            }

            _hookExecutor.AfterReadMany(resources);
            return _hookExecutor.OnReturnMany(resources);
        }

        /// <inheritdoc />
        public virtual async Task<TResource> GetAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            _hookExecutor.BeforeReadSingle<TResource, TId>(id, ResourcePipeline.GetSingle);

            var primaryResource = await TryGetPrimaryResourceByIdAsync(id, TopFieldSelection.PreserveExisting);
            AssertPrimaryResourceExists(primaryResource);

            _hookExecutor.AfterReadSingle(primaryResource, ResourcePipeline.GetSingle);
            _hookExecutor.OnReturnSingle(primaryResource, ResourcePipeline.GetSingle);

            return primaryResource;
        }

        /// <inheritdoc />
        public virtual async Task<object> GetSecondaryAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            AssertHasRelationship(_request.Relationship, relationshipName);

            _hookExecutor.BeforeReadSingle<TResource, TId>(id, ResourcePipeline.GetRelationship);

            var secondaryLayer = _queryLayerComposer.ComposeFromConstraints(_request.SecondaryResource);
            var primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            if (_request.IsCollection && _options.IncludeTotalResourceCount)
            {
                // TODO: Consider support for pagination links on secondary resource collection. This requires to call Count() on the inverse relationship (which may not exist).
                // For /blogs/1/articles we need to execute Count(Articles.Where(article => article.Blog.Id == 1 && article.Blog.existingFilter))) to determine TotalResourceCount.
                // This also means we need to invoke ResourceRepository<Article>.CountAsync() from ResourceService<Blog>.
                // And we should call BlogResourceDefinition.OnApplyFilter to filter out soft-deleted blogs and translate from equals('IsDeleted','false') to equals('Blog.IsDeleted','false')
            }

            var primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer);

            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            _hookExecutor.AfterReadSingle(primaryResource, ResourcePipeline.GetRelationship);

            var secondaryResourceOrResources = _request.Relationship.GetValue(primaryResource);

            if (secondaryResourceOrResources is ICollection secondaryResources &&
                secondaryLayer.Pagination?.PageSize?.Value == secondaryResources.Count)
            {
                _paginationContext.IsPageFull = true;
            }

            return _hookExecutor.OnReturnRelationship(secondaryResourceOrResources);
        }

        /// <inheritdoc />
        public virtual async Task<object> GetRelationshipAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            AssertHasRelationship(_request.Relationship, relationshipName);

            _hookExecutor.BeforeReadSingle<TResource, TId>(id, ResourcePipeline.GetRelationship);

            var secondaryLayer = _queryLayerComposer.ComposeSecondaryLayerForRelationship(_request.SecondaryResource);
            var primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            var primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer);

            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            _hookExecutor.AfterReadSingle(primaryResource, ResourcePipeline.GetRelationship);

            var secondaryResourceOrResources = _request.Relationship.GetValue(primaryResource);

            return _hookExecutor.OnReturnRelationship(secondaryResourceOrResources);
        }

        /// <inheritdoc />
        public virtual async Task<TResource> CreateAsync(TResource resource)
        {
            _traceWriter.LogMethodStart(new {resource});
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            _resourceChangeTracker.SetRequestedAttributeValues(resource);

            var defaultResource = _resourceFactory.CreateInstance<TResource>();
            defaultResource.Id = resource.Id;

            _resourceChangeTracker.SetInitiallyStoredAttributeValues(defaultResource);

            _hookExecutor.BeforeCreate(resource);

            try
            {
                await _repositoryAccessor.CreateAsync(resource);
            }
            catch (DataStoreUpdateException)
            {
                var existingResource = await TryGetPrimaryResourceByIdAsync(resource.Id, TopFieldSelection.OnlyIdAttribute);
                if (existingResource != null)
                {
                    throw new ResourceAlreadyExistsException(resource.StringId, _request.PrimaryResource.PublicName);
                }

                await AssertResourcesToAssignInRelationshipsExistAsync(resource);
                throw;
            }

            var resourceFromDatabase = await TryGetPrimaryResourceByIdAsync(resource.Id, TopFieldSelection.WithAllAttributes);
            AssertPrimaryResourceExists(resourceFromDatabase);

            _hookExecutor.AfterCreate(resourceFromDatabase);

            _resourceChangeTracker.SetFinallyStoredAttributeValues(resourceFromDatabase);

            bool hasImplicitChanges = _resourceChangeTracker.HasImplicitChanges();
            if (!hasImplicitChanges)
            {
                return null;
            }

            _hookExecutor.OnReturnSingle(resourceFromDatabase, ResourcePipeline.Post);
            return resourceFromDatabase;
        }

        private async Task AssertResourcesToAssignInRelationshipsExistAsync(TResource resource)
        {
            var missingResources = new List<MissingResourceInRelationship>();

            foreach (var (queryLayer, relationship) in _queryLayerComposer.ComposeForGetTargetedSecondaryResourceIds(resource))
            {
                object rightValue = relationship.GetValue(resource);
                ICollection<IIdentifiable> rightResourceIds = TypeHelper.ExtractResources(rightValue);

                var missingResourcesInRelationship = GetMissingRightResourcesAsync(queryLayer, relationship, rightResourceIds);
                await missingResources.AddRangeAsync(missingResourcesInRelationship);
            }

            if (missingResources.Any())
            {
                throw new ResourcesInRelationshipsNotFoundException(missingResources);
            }
        }

        private async IAsyncEnumerable<MissingResourceInRelationship> GetMissingRightResourcesAsync(
            QueryLayer existingRightResourceIdsQueryLayer, RelationshipAttribute relationship,
            ICollection<IIdentifiable> rightResourceIds)
        {
            var existingResources = await _repositoryAccessor.GetAsync(
                existingRightResourceIdsQueryLayer.ResourceContext.ResourceType, existingRightResourceIdsQueryLayer);

            var existingResourceIds = existingResources.Select(resource => resource.StringId).ToArray();

            foreach (var rightResourceId in rightResourceIds)
            {
                if (!existingResourceIds.Contains(rightResourceId.StringId))
                {
                    yield return new MissingResourceInRelationship(relationship.PublicName,
                        existingRightResourceIdsQueryLayer.ResourceContext.PublicName, rightResourceId.StringId);
                }
            }
        }

        /// <inheritdoc />
        public async Task AddToToManyRelationshipAsync(TId primaryId, string relationshipName, ISet<IIdentifiable> secondaryResourceIds)
        {
            _traceWriter.LogMethodStart(new {primaryId, secondaryResourceIds});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));
            if (secondaryResourceIds == null) throw new ArgumentNullException(nameof(secondaryResourceIds));

            AssertHasRelationship(_request.Relationship, relationshipName);

            if (secondaryResourceIds.Any())
            {
                if (_request.Relationship is HasManyThroughAttribute hasManyThrough)
                {
                    // In the case of a many-to-many relationship, creating a duplicate entry in the join table results in a
                    // unique constraint violation. We avoid that by excluding already-existing entries from the set in advance.
                    await RemoveExistingIdsFromSecondarySet(primaryId, secondaryResourceIds, hasManyThrough);
                }

                try
                {
                    await _repositoryAccessor.AddToToManyRelationshipAsync(typeof(TResource), primaryId, secondaryResourceIds);
                }
                catch (DataStoreUpdateException)
                {
                    var primaryResource = await TryGetPrimaryResourceByIdAsync(primaryId, TopFieldSelection.OnlyIdAttribute);
                    AssertPrimaryResourceExists(primaryResource);

                    await AssertResourcesExistAsync(secondaryResourceIds);
                    throw;
                }
            }
        }

        private async Task RemoveExistingIdsFromSecondarySet(TId primaryId, ISet<IIdentifiable> secondaryResourceIds,
            HasManyThroughAttribute hasManyThrough)
        {
            var queryLayer = _queryLayerComposer.ComposeForHasManyThrough(hasManyThrough, primaryId, secondaryResourceIds);
            var primaryResources = await _repositoryAccessor.GetAsync<TResource>(queryLayer);
            
            var primaryResource = primaryResources.FirstOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            var rightValue = _request.Relationship.GetValue(primaryResource);
            var existingRightResourceIds = TypeHelper.ExtractResources(rightValue);

            secondaryResourceIds.ExceptWith(existingRightResourceIds);
        }

        private async Task AssertResourcesExistAsync(ICollection<IIdentifiable> secondaryResourceIds)
        {
            var queryLayer = _queryLayerComposer.ComposeForGetRelationshipRightIds(_request.Relationship, secondaryResourceIds);

            var missingResources = await GetMissingRightResourcesAsync(queryLayer, _request.Relationship, secondaryResourceIds).ToListAsync();
            if (missingResources.Any())
            {
                throw new ResourcesInRelationshipsNotFoundException(missingResources);
            }
        }

        /// <inheritdoc />
        public virtual async Task<TResource> UpdateAsync(TId id, TResource resource)
        {
            _traceWriter.LogMethodStart(new {id, resource});
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            var resourceFromRequest = resource;
            _resourceChangeTracker.SetRequestedAttributeValues(resourceFromRequest);

            _hookExecutor.BeforeUpdateResource(resourceFromRequest);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(id);

            _resourceChangeTracker.SetInitiallyStoredAttributeValues(resourceFromDatabase);

            try
            {
                await _repositoryAccessor.UpdateAsync(resourceFromRequest, resourceFromDatabase);
            }
            catch (DataStoreUpdateException)
            {
                await AssertResourcesToAssignInRelationshipsExistAsync(resourceFromRequest);
                throw;
            }

            TResource afterResourceFromDatabase = await TryGetPrimaryResourceByIdAsync(id, TopFieldSelection.WithAllAttributes);
            AssertPrimaryResourceExists(afterResourceFromDatabase);

            _hookExecutor.AfterUpdateResource(afterResourceFromDatabase);

            _resourceChangeTracker.SetFinallyStoredAttributeValues(afterResourceFromDatabase);

            bool hasImplicitChanges = _resourceChangeTracker.HasImplicitChanges();
            if (!hasImplicitChanges)
            {
                return null;
            }

            _hookExecutor.OnReturnSingle(afterResourceFromDatabase, ResourcePipeline.Patch);
            return afterResourceFromDatabase;
        }

        /// <inheritdoc />
        public virtual async Task SetRelationshipAsync(TId primaryId, string relationshipName, object secondaryResourceIds)
        {
            _traceWriter.LogMethodStart(new {primaryId, relationshipName, secondaryResourceIds});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            AssertHasRelationship(_request.Relationship, relationshipName);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(primaryId);

            _hookExecutor.BeforeUpdateRelationship(resourceFromDatabase);

            try
            {
                await _repositoryAccessor.SetRelationshipAsync(resourceFromDatabase, secondaryResourceIds);
            }
            catch (DataStoreUpdateException)
            {
                await AssertResourcesExistAsync(TypeHelper.ExtractResources(secondaryResourceIds));
                throw;
            }

            _hookExecutor.AfterUpdateRelationship(resourceFromDatabase);
        }

        /// <inheritdoc />
        public virtual async Task DeleteAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            _hookExecutor.BeforeDelete<TResource, TId>(id);

            try
            {
                await _repositoryAccessor.DeleteAsync(typeof(TResource), id);
            }
            catch (DataStoreUpdateException)
            {
                var primaryResource = await TryGetPrimaryResourceByIdAsync(id, TopFieldSelection.OnlyIdAttribute);
                AssertPrimaryResourceExists(primaryResource);
                throw;
            }

            _hookExecutor.AfterDelete<TResource, TId>(id);
        }

        /// <inheritdoc />
        public async Task RemoveFromToManyRelationshipAsync(TId primaryId, string relationshipName, ISet<IIdentifiable> secondaryResourceIds)
        {
            _traceWriter.LogMethodStart(new {primaryId, relationshipName, secondaryResourceIds});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));
            if (secondaryResourceIds == null) throw new ArgumentNullException(nameof(secondaryResourceIds));

            AssertHasRelationship(_request.Relationship, relationshipName);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(primaryId);
            await AssertResourcesExistAsync(secondaryResourceIds);

            if (secondaryResourceIds.Any())
            {
                await _repositoryAccessor.RemoveFromToManyRelationshipAsync(resourceFromDatabase, secondaryResourceIds);
            }
        }

        private async Task<TResource> TryGetPrimaryResourceByIdAsync(TId id, TopFieldSelection fieldSelection)
        {
            var primaryLayer = _queryLayerComposer.ComposeForGetById(id, _request.PrimaryResource, fieldSelection);

            var primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer);
            return primaryResources.SingleOrDefault();
        }

        private async Task<TResource> GetPrimaryResourceForUpdateAsync(TId id)
        {
            var queryLayer = _queryLayerComposer.ComposeForUpdate(id, _request.PrimaryResource);
            var resource = await _repositoryAccessor.GetForUpdateAsync<TResource>(queryLayer);

            AssertPrimaryResourceExists(resource);
            return resource;
        }

        private void AssertPrimaryResourceExists(TResource resource)
        {
            if (resource == null)
            {
                throw new ResourceNotFoundException(_request.PrimaryId, _request.PrimaryResource.PublicName);
            }
        }

        private void AssertHasRelationship(RelationshipAttribute relationship, string name)
        {
            if (relationship == null)
            {
                throw new RelationshipNotFoundException(name, _request.PrimaryResource.PublicName);
            }
        }
    }

    /// <summary>
    /// Represents the foundational Resource Service layer in the JsonApiDotNetCore architecture that uses a Resource Repository for data access.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    public class JsonApiResourceService<TResource> : JsonApiResourceService<TResource, int>,
        IResourceService<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public JsonApiResourceService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceFactory resourceFactory,
            IResourceHookExecutorFacade hookExecutor)
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceFactory, hookExecutor)
        {
        }
    }
}
