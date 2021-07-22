using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Services
{
    /// <inheritdoc />
    [PublicAPI]
    public class JsonApiResourceService<TResource, TId> : IResourceService<TResource, TId>
        where TResource : class, IIdentifiable<TId>
    {
        private readonly CollectionConverter _collectionConverter = new();
        private readonly IResourceRepositoryAccessor _repositoryAccessor;
        private readonly IQueryLayerComposer _queryLayerComposer;
        private readonly IPaginationContext _paginationContext;
        private readonly IJsonApiOptions _options;
        private readonly TraceLogWriter<JsonApiResourceService<TResource, TId>> _traceWriter;
        private readonly IJsonApiRequest _request;
        private readonly IResourceChangeTracker<TResource> _resourceChangeTracker;
        private readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;

        public JsonApiResourceService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory, IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker)
        {
            ArgumentGuard.NotNull(repositoryAccessor, nameof(repositoryAccessor));
            ArgumentGuard.NotNull(queryLayerComposer, nameof(queryLayerComposer));
            ArgumentGuard.NotNull(paginationContext, nameof(paginationContext));
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(loggerFactory, nameof(loggerFactory));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(resourceChangeTracker, nameof(resourceChangeTracker));

            _repositoryAccessor = repositoryAccessor;
            _queryLayerComposer = queryLayerComposer;
            _paginationContext = paginationContext;
            _options = options;
            _request = request;
            _resourceChangeTracker = resourceChangeTracker;
            _traceWriter = new TraceLogWriter<JsonApiResourceService<TResource, TId>>(loggerFactory);

#pragma warning disable 612 // Method is obsolete
            _resourceDefinitionAccessor = queryLayerComposer.GetResourceDefinitionAccessor();
#pragma warning restore 612
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyCollection<TResource>> GetAsync(CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart();

            if (_options.IncludeTotalResourceCount)
            {
                FilterExpression topFilter = _queryLayerComposer.GetTopFilterFromConstraints(_request.PrimaryResource);
                _paginationContext.TotalResourceCount = await _repositoryAccessor.CountAsync<TResource>(topFilter, cancellationToken);

                if (_paginationContext.TotalResourceCount == 0)
                {
                    return Array.Empty<TResource>();
                }
            }

            QueryLayer queryLayer = _queryLayerComposer.ComposeFromConstraints(_request.PrimaryResource);
            IReadOnlyCollection<TResource> resources = await _repositoryAccessor.GetAsync<TResource>(queryLayer, cancellationToken);

            if (queryLayer.Pagination?.PageSize != null && queryLayer.Pagination.PageSize.Value == resources.Count)
            {
                _paginationContext.IsPageFull = true;
            }

            return resources;
        }

        /// <inheritdoc />
        public virtual async Task<TResource> GetAsync(TId id, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id
            });

            return await GetPrimaryResourceByIdAsync(id, TopFieldSelection.PreserveExisting, cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<object> GetSecondaryAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName
            });

            AssertHasRelationship(_request.Relationship, relationshipName);

            QueryLayer secondaryLayer = _queryLayerComposer.ComposeFromConstraints(_request.SecondaryResource);
            QueryLayer primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            IReadOnlyCollection<TResource> primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer, cancellationToken);

            TResource primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            object secondaryResourceOrResources = _request.Relationship.GetValue(primaryResource);

            if (secondaryResourceOrResources is ICollection secondaryResources && secondaryLayer.Pagination?.PageSize?.Value == secondaryResources.Count)
            {
                _paginationContext.IsPageFull = true;
            }

            return secondaryResourceOrResources;
        }

        /// <inheritdoc />
        public virtual async Task<object> GetRelationshipAsync(TId id, string relationshipName, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                relationshipName
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));

            AssertHasRelationship(_request.Relationship, relationshipName);

            QueryLayer secondaryLayer = _queryLayerComposer.ComposeSecondaryLayerForRelationship(_request.SecondaryResource);
            QueryLayer primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            IReadOnlyCollection<TResource> primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer, cancellationToken);

            TResource primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            return _request.Relationship.GetValue(primaryResource);
        }

        /// <inheritdoc />
        public virtual async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                resource
            });

            ArgumentGuard.NotNull(resource, nameof(resource));

            TResource resourceFromRequest = resource;
            _resourceChangeTracker.SetRequestedAttributeValues(resourceFromRequest);

            TResource resourceForDatabase = await _repositoryAccessor.GetForCreateAsync<TResource, TId>(resource.Id, cancellationToken);

            _resourceChangeTracker.SetInitiallyStoredAttributeValues(resourceForDatabase);

            await InitializeResourceAsync(resourceForDatabase, cancellationToken);

            try
            {
                await _repositoryAccessor.CreateAsync(resourceFromRequest, resourceForDatabase, cancellationToken);
            }
            catch (DataStoreUpdateException)
            {
                if (!Equals(resourceFromRequest.Id, default(TId)))
                {
                    TResource existingResource =
                        await TryGetPrimaryResourceByIdAsync(resourceFromRequest.Id, TopFieldSelection.OnlyIdAttribute, cancellationToken);

                    if (existingResource != null)
                    {
                        throw new ResourceAlreadyExistsException(resourceFromRequest.StringId, _request.PrimaryResource.PublicName);
                    }
                }

                await AssertResourcesToAssignInRelationshipsExistAsync(resourceFromRequest, cancellationToken);
                throw;
            }

            TResource resourceFromDatabase = await GetPrimaryResourceByIdAsync(resourceForDatabase.Id, TopFieldSelection.WithAllAttributes, cancellationToken);

            _resourceChangeTracker.SetFinallyStoredAttributeValues(resourceFromDatabase);

            bool hasImplicitChanges = _resourceChangeTracker.HasImplicitChanges();
            return hasImplicitChanges ? resourceFromDatabase : null;
        }

        protected virtual async Task InitializeResourceAsync(TResource resourceForDatabase, CancellationToken cancellationToken)
        {
            await _resourceDefinitionAccessor.OnPrepareWriteAsync(resourceForDatabase, OperationKind.CreateResource, cancellationToken);
        }

        protected async Task AssertResourcesToAssignInRelationshipsExistAsync(TResource primaryResource, CancellationToken cancellationToken)
        {
            var missingResources = new List<MissingResourceInRelationship>();

            foreach ((QueryLayer queryLayer, RelationshipAttribute relationship) in _queryLayerComposer.ComposeForGetTargetedSecondaryResourceIds(
                primaryResource))
            {
                object rightValue = relationship.GetValue(primaryResource);
                ICollection<IIdentifiable> rightResourceIds = _collectionConverter.ExtractResources(rightValue);

                IAsyncEnumerable<MissingResourceInRelationship> missingResourcesInRelationship =
                    GetMissingRightResourcesAsync(queryLayer, relationship, rightResourceIds, cancellationToken);

                await missingResources.AddRangeAsync(missingResourcesInRelationship, cancellationToken);
            }

            if (missingResources.Any())
            {
                throw new ResourcesInRelationshipsNotFoundException(missingResources);
            }
        }

        private async IAsyncEnumerable<MissingResourceInRelationship> GetMissingRightResourcesAsync(QueryLayer existingRightResourceIdsQueryLayer,
            RelationshipAttribute relationship, ICollection<IIdentifiable> rightResourceIds, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IReadOnlyCollection<IIdentifiable> existingResources = await _repositoryAccessor.GetAsync(
                existingRightResourceIdsQueryLayer.ResourceContext.ResourceType, existingRightResourceIdsQueryLayer, cancellationToken);

            string[] existingResourceIds = existingResources.Select(resource => resource.StringId).ToArray();

            foreach (IIdentifiable rightResourceId in rightResourceIds)
            {
                if (!existingResourceIds.Contains(rightResourceId.StringId))
                {
                    yield return new MissingResourceInRelationship(relationship.PublicName, existingRightResourceIdsQueryLayer.ResourceContext.PublicName,
                        rightResourceId.StringId);
                }
            }
        }

        /// <inheritdoc />
        public virtual async Task AddToToManyRelationshipAsync(TId primaryId, string relationshipName, ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                primaryId,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));
            ArgumentGuard.NotNull(secondaryResourceIds, nameof(secondaryResourceIds));

            AssertHasRelationship(_request.Relationship, relationshipName);

            if (secondaryResourceIds.Any() && _request.Relationship is HasManyThroughAttribute hasManyThrough)
            {
                // In the case of a many-to-many relationship, creating a duplicate entry in the join table results in a
                // unique constraint violation. We avoid that by excluding already-existing entries from the set in advance.
                await RemoveExistingIdsFromSecondarySetAsync(primaryId, secondaryResourceIds, hasManyThrough, cancellationToken);
            }

            try
            {
                await _repositoryAccessor.AddToToManyRelationshipAsync<TResource, TId>(primaryId, secondaryResourceIds, cancellationToken);
            }
            catch (DataStoreUpdateException)
            {
                _ = await GetPrimaryResourceByIdAsync(primaryId, TopFieldSelection.OnlyIdAttribute, cancellationToken);
                await AssertRightResourcesExistAsync(secondaryResourceIds, cancellationToken);
                throw;
            }
        }

        private async Task RemoveExistingIdsFromSecondarySetAsync(TId primaryId, ISet<IIdentifiable> secondaryResourceIds,
            HasManyThroughAttribute hasManyThrough, CancellationToken cancellationToken)
        {
            QueryLayer queryLayer = _queryLayerComposer.ComposeForHasMany(hasManyThrough, primaryId, secondaryResourceIds);
            IReadOnlyCollection<TResource> primaryResources = await _repositoryAccessor.GetAsync<TResource>(queryLayer, cancellationToken);

            TResource primaryResource = primaryResources.FirstOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            object rightValue = _request.Relationship.GetValue(primaryResource);
            ICollection<IIdentifiable> existingRightResourceIds = _collectionConverter.ExtractResources(rightValue);

            secondaryResourceIds.ExceptWith(existingRightResourceIds);
        }

        protected async Task AssertRightResourcesExistAsync(object rightResourceIds, CancellationToken cancellationToken)
        {
            ICollection<IIdentifiable> secondaryResourceIds = _collectionConverter.ExtractResources(rightResourceIds);

            if (secondaryResourceIds.Any())
            {
                QueryLayer queryLayer = _queryLayerComposer.ComposeForGetRelationshipRightIds(_request.Relationship, secondaryResourceIds);

                List<MissingResourceInRelationship> missingResources =
                    await GetMissingRightResourcesAsync(queryLayer, _request.Relationship, secondaryResourceIds, cancellationToken)
                        .ToListAsync(cancellationToken);

                if (missingResources.Any())
                {
                    throw new ResourcesInRelationshipsNotFoundException(missingResources);
                }
            }
        }

        /// <inheritdoc />
        public virtual async Task<TResource> UpdateAsync(TId id, TResource resource, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id,
                resource
            });

            ArgumentGuard.NotNull(resource, nameof(resource));

            TResource resourceFromRequest = resource;
            _resourceChangeTracker.SetRequestedAttributeValues(resourceFromRequest);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(id, cancellationToken);

            _resourceChangeTracker.SetInitiallyStoredAttributeValues(resourceFromDatabase);

            await _resourceDefinitionAccessor.OnPrepareWriteAsync(resourceFromDatabase, OperationKind.UpdateResource, cancellationToken);

            try
            {
                await _repositoryAccessor.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
            }
            catch (DataStoreUpdateException)
            {
                await AssertResourcesToAssignInRelationshipsExistAsync(resourceFromRequest, cancellationToken);
                throw;
            }

            TResource afterResourceFromDatabase = await GetPrimaryResourceByIdAsync(id, TopFieldSelection.WithAllAttributes, cancellationToken);

            _resourceChangeTracker.SetFinallyStoredAttributeValues(afterResourceFromDatabase);

            bool hasImplicitChanges = _resourceChangeTracker.HasImplicitChanges();
            return hasImplicitChanges ? afterResourceFromDatabase : null;
        }

        /// <inheritdoc />
        public virtual async Task SetRelationshipAsync(TId primaryId, string relationshipName, object secondaryResourceIds, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                primaryId,
                relationshipName,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));

            AssertHasRelationship(_request.Relationship, relationshipName);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(primaryId, cancellationToken);

            await _resourceDefinitionAccessor.OnPrepareWriteAsync(resourceFromDatabase, OperationKind.SetRelationship, cancellationToken);

            try
            {
                await _repositoryAccessor.SetRelationshipAsync(resourceFromDatabase, secondaryResourceIds, cancellationToken);
            }
            catch (DataStoreUpdateException)
            {
                await AssertRightResourcesExistAsync(secondaryResourceIds, cancellationToken);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                id
            });

            try
            {
                await _repositoryAccessor.DeleteAsync<TResource, TId>(id, cancellationToken);
            }
            catch (DataStoreUpdateException)
            {
                _ = await GetPrimaryResourceByIdAsync(id, TopFieldSelection.OnlyIdAttribute, cancellationToken);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task RemoveFromToManyRelationshipAsync(TId primaryId, string relationshipName, ISet<IIdentifiable> secondaryResourceIds,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                primaryId,
                relationshipName,
                secondaryResourceIds
            });

            ArgumentGuard.NotNullNorEmpty(relationshipName, nameof(relationshipName));
            ArgumentGuard.NotNull(secondaryResourceIds, nameof(secondaryResourceIds));

            AssertHasRelationship(_request.Relationship, relationshipName);

            TResource resourceFromDatabase = await GetPrimaryResourceForUpdateAsync(primaryId, cancellationToken);

            await _resourceDefinitionAccessor.OnPrepareWriteAsync(resourceFromDatabase, OperationKind.RemoveFromRelationship, cancellationToken);

            await AssertRightResourcesExistAsync(secondaryResourceIds, cancellationToken);

            await _repositoryAccessor.RemoveFromToManyRelationshipAsync(resourceFromDatabase, secondaryResourceIds, cancellationToken);
        }

        protected async Task<TResource> GetPrimaryResourceByIdAsync(TId id, TopFieldSelection fieldSelection, CancellationToken cancellationToken)
        {
            TResource primaryResource = await TryGetPrimaryResourceByIdAsync(id, fieldSelection, cancellationToken);
            AssertPrimaryResourceExists(primaryResource);

            return primaryResource;
        }

        private async Task<TResource> TryGetPrimaryResourceByIdAsync(TId id, TopFieldSelection fieldSelection, CancellationToken cancellationToken)
        {
            QueryLayer primaryLayer = _queryLayerComposer.ComposeForGetById(id, _request.PrimaryResource, fieldSelection);

            IReadOnlyCollection<TResource> primaryResources = await _repositoryAccessor.GetAsync<TResource>(primaryLayer, cancellationToken);
            return primaryResources.SingleOrDefault();
        }

        protected async Task<TResource> GetPrimaryResourceForUpdateAsync(TId id, CancellationToken cancellationToken)
        {
            QueryLayer queryLayer = _queryLayerComposer.ComposeForUpdate(id, _request.PrimaryResource);
            var resource = await _repositoryAccessor.GetForUpdateAsync<TResource>(queryLayer, cancellationToken);

            AssertPrimaryResourceExists(resource);
            return resource;
        }

        [AssertionMethod]
        private void AssertPrimaryResourceExists(TResource resource)
        {
            if (resource == null)
            {
                throw new ResourceNotFoundException(_request.PrimaryId, _request.PrimaryResource.PublicName);
            }
        }

        [AssertionMethod]
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
    /// <typeparam name="TResource">
    /// The resource type.
    /// </typeparam>
    [PublicAPI]
    public class JsonApiResourceService<TResource> : JsonApiResourceService<TResource, int>, IResourceService<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public JsonApiResourceService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory, IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker)
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker)
        {
        }
    }
}
