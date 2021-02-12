using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal.QueryableBuilding;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Repositories
{
    /// <summary>
    /// Implements the foundational Repository layer in the JsonApiDotNetCore architecture that uses Entity Framework Core.
    /// </summary>
    public class EntityFrameworkCoreRepository<TResource, TId> : IResourceRepository<TResource, TId>, IRepositorySupportsTransaction
        where TResource : class, IIdentifiable<TId>
    {
        private readonly ITargetedFields _targetedFields;
        private readonly DbContext _dbContext;
        private readonly IResourceGraph _resourceGraph;
        private readonly IResourceFactory _resourceFactory;
        private readonly IEnumerable<IQueryConstraintProvider> _constraintProviders;
        private readonly TraceLogWriter<EntityFrameworkCoreRepository<TResource, TId>> _traceWriter;

        /// <inheritdoc />
        public virtual Guid? TransactionId => _dbContext.Database.CurrentTransaction?.TransactionId;

        public EntityFrameworkCoreRepository(
            ITargetedFields targetedFields,
            IDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory)
        {
            if (contextResolver == null) throw new ArgumentNullException(nameof(contextResolver));
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _targetedFields = targetedFields ?? throw new ArgumentNullException(nameof(targetedFields));
            _resourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
            _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
            _constraintProviders = constraintProviders ?? throw new ArgumentNullException(nameof(constraintProviders));

            _dbContext = contextResolver.GetContext();
            _traceWriter = new TraceLogWriter<EntityFrameworkCoreRepository<TResource, TId>>(loggerFactory);
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyCollection<TResource>> GetAsync(QueryLayer layer, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {layer});
            if (layer == null) throw new ArgumentNullException(nameof(layer));

            IQueryable<TResource> query = ApplyQueryLayer(layer);
            return await query.ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<int> CountAsync(FilterExpression topFilter, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {topFilter});

            var resourceContext = _resourceGraph.GetResourceContext<TResource>();
            var layer = new QueryLayer(resourceContext)
            {
                Filter = topFilter
            };

            IQueryable<TResource> query = ApplyQueryLayer(layer);
            return await query.CountAsync(cancellationToken);
        }

        protected virtual IQueryable<TResource> ApplyQueryLayer(QueryLayer layer)
        {
            _traceWriter.LogMethodStart(new {layer});
            if (layer == null) throw new ArgumentNullException(nameof(layer));

            if (EntityFrameworkCoreSupport.Version.Major < 5)
            {
                var writer = new MemoryLeakDetectionBugRewriter();
                layer = writer.Rewrite(layer);
            }

            IQueryable<TResource> source = GetAll();

            var queryableHandlers = _constraintProviders
                .SelectMany(p => p.GetConstraints())
                .Where(expressionInScope => expressionInScope.Scope == null)
                .Select(expressionInScope => expressionInScope.Expression)
                .OfType<QueryableHandlerExpression>()
                .ToArray();

            foreach (var queryableHandler in queryableHandlers)
            {
                source = queryableHandler.Apply(source);
            }

            var nameFactory = new LambdaParameterNameFactory();
            var builder = new QueryableBuilder(source.Expression, source.ElementType, typeof(Queryable), nameFactory, _resourceFactory, _resourceGraph, _dbContext.Model);

            var expression = builder.ApplyQuery(layer);
            return source.Provider.CreateQuery<TResource>(expression);
        }

        protected virtual IQueryable<TResource> GetAll()
        {
            return _dbContext.Set<TResource>();
        }

        /// <inheritdoc />
        public virtual Task<TResource> GetForCreateAsync(TId id, CancellationToken cancellationToken)
        {
            var resource = _resourceFactory.CreateInstance<TResource>();
            resource.Id = id;

            return Task.FromResult(resource);
        }

        /// <inheritdoc />
        public virtual async Task CreateAsync(TResource resourceFromRequest, TResource resourceForDatabase, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {resourceFromRequest, resourceForDatabase});
            if (resourceFromRequest == null) throw new ArgumentNullException(nameof(resourceFromRequest));
            if (resourceForDatabase == null) throw new ArgumentNullException(nameof(resourceForDatabase));

            using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);

            foreach (var relationship in _targetedFields.Relationships)
            {
                var rightResources = relationship.GetValue(resourceFromRequest);
                await UpdateRelationshipAsync(relationship, resourceForDatabase, rightResources, collector, cancellationToken);
            }

            foreach (var attribute in _targetedFields.Attributes)
            {
                attribute.SetValue(resourceForDatabase, attribute.GetValue(resourceFromRequest));
            }

            var dbSet = _dbContext.Set<TResource>();
            dbSet.Add(resourceForDatabase);

            await SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<TResource> GetForUpdateAsync(QueryLayer queryLayer, CancellationToken cancellationToken)
        {
            var resources = await GetAsync(queryLayer, cancellationToken);
            return resources.FirstOrDefault();
        }

        /// <inheritdoc />
        public virtual async Task UpdateAsync(TResource resourceFromRequest, TResource resourceFromDatabase, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {resourceFromRequest, resourceFromDatabase});
            if (resourceFromRequest == null) throw new ArgumentNullException(nameof(resourceFromRequest));
            if (resourceFromDatabase == null) throw new ArgumentNullException(nameof(resourceFromDatabase));

            using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);

            foreach (var relationship in _targetedFields.Relationships)
            {
                var rightResources = relationship.GetValue(resourceFromRequest);

                AssertIsNotClearingRequiredRelationship(relationship, resourceFromDatabase, rightResources);

                await UpdateRelationshipAsync(relationship, resourceFromDatabase, rightResources, collector, cancellationToken);
            }

            foreach (var attribute in _targetedFields.Attributes)
            {
                attribute.SetValue(resourceFromDatabase, attribute.GetValue(resourceFromRequest));
            }

            RestoreConcurrencyToken(resourceFromRequest, resourceFromDatabase);

            await SaveChangesAsync(cancellationToken);
        }

        private void RestoreConcurrencyToken(TResource resourceFromRequest, TResource resourceFromDatabase)
        {
            foreach (var propertyEntry in _dbContext.Entry(resourceFromDatabase).Properties)
            {
                if (propertyEntry.Metadata.IsConcurrencyToken)
                {
                    // Overwrite the ConcurrencyToken coming from database with the one from the request body.
                    // If they are different, EF Core throws a DbUpdateConcurrencyException on save.

                    var concurrencyTokenProperty = typeof(TResource).GetProperty(propertyEntry.Metadata.PropertyInfo.Name);
                    if (concurrencyTokenProperty != null)
                    {
                        var concurrencyTokenFromRequest = concurrencyTokenProperty.GetValue(resourceFromRequest);
                        propertyEntry.OriginalValue = concurrencyTokenFromRequest;
                    }
                }
            }
        }

        protected void AssertIsNotClearingRequiredRelationship(RelationshipAttribute relationship, TResource leftResource, object rightValue)
        {
            bool relationshipIsRequired = false;

            if (!(relationship is HasManyThroughAttribute))
            {
                var navigation = TryGetNavigation(relationship);
                relationshipIsRequired = navigation?.ForeignKey?.IsRequired ?? false;
            }

            var relationshipIsBeingCleared = relationship is HasOneAttribute
                ? rightValue == null
                : IsToManyRelationshipBeingCleared(relationship, leftResource, rightValue);
            
            if (relationshipIsRequired && relationshipIsBeingCleared)
            {
                var resourceType = _resourceGraph.GetResourceContext<TResource>().PublicName;
                throw new CannotClearRequiredRelationshipException(relationship.PublicName, leftResource.StringId, resourceType);
            }
        }

        private static bool IsToManyRelationshipBeingCleared(RelationshipAttribute relationship, TResource leftResource, object valueToAssign)
        {
            ICollection<IIdentifiable> newRightResourceIds = TypeHelper.ExtractResources(valueToAssign);

            var existingRightValue = relationship.GetValue(leftResource);
            var existingRightResourceIds = TypeHelper.ExtractResources(existingRightValue).ToHashSet(IdentifiableComparer.Instance);

            existingRightResourceIds.ExceptWith(newRightResourceIds);

            return existingRightResourceIds.Any();
        }

        /// <inheritdoc />
        public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {id});

            using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);
            var resource = collector.CreateForId<TResource, TId>(id);

            foreach (var relationship in _resourceGraph.GetRelationships<TResource>())
            {
                // Loads the data of the relationship, if in EF Core it is configured in such a way that loading the related
                // entities into memory is required for successfully executing the selected deletion behavior. 
                if (RequiresLoadOfRelationshipForDeletion(relationship))
                {
                    var navigation = GetNavigationEntry(resource, relationship);
                    await navigation.LoadAsync(cancellationToken);
                }
            }

            _dbContext.Remove(resource);

            await SaveChangesAsync(cancellationToken);
        }

        private NavigationEntry GetNavigationEntry(TResource resource, RelationshipAttribute relationship)
        {
            EntityEntry<TResource> entityEntry = _dbContext.Entry(resource);

            switch (relationship)
            {
                case HasOneAttribute hasOneRelationship:
                {
                    return entityEntry.Reference(hasOneRelationship.Property.Name);
                }
                case HasManyAttribute hasManyRelationship:
                {
                    return entityEntry.Collection(hasManyRelationship.Property.Name);
                }
                default:
                {
                    throw new InvalidOperationException($"Unknown relationship type '{relationship.GetType().Name}'.");
                }
            }
        }

        private bool RequiresLoadOfRelationshipForDeletion(RelationshipAttribute relationship)
        {
            var navigation = TryGetNavigation(relationship);
            bool isClearOfForeignKeyRequired = navigation?.ForeignKey.DeleteBehavior == DeleteBehavior.ClientSetNull;

            bool hasForeignKeyAtLeftSide = HasForeignKeyAtLeftSide(relationship);

            return isClearOfForeignKeyRequired && !hasForeignKeyAtLeftSide;
        }

        private INavigation TryGetNavigation(RelationshipAttribute relationship)
        {
            var entityType = _dbContext.Model.FindEntityType(typeof(TResource));
            return entityType?.FindNavigation(relationship.Property.Name);
        }

        private bool HasForeignKeyAtLeftSide(RelationshipAttribute relationship)
        {
            if (relationship is HasOneAttribute)
            {
                var navigation = TryGetNavigation(relationship);
                return navigation?.IsDependentToPrincipal() ?? false;
            }

            return false;
        }

        /// <inheritdoc />
        public virtual async Task SetRelationshipAsync(TResource primaryResource, object secondaryResourceIds, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {primaryResource, secondaryResourceIds});

            var relationship = _targetedFields.Relationships.Single();

            AssertIsNotClearingRequiredRelationship(relationship, primaryResource, secondaryResourceIds);

            using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);
            await UpdateRelationshipAsync(relationship, primaryResource, secondaryResourceIds, collector, cancellationToken);

            await SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task AddToToManyRelationshipAsync(TId primaryId, ISet<IIdentifiable> secondaryResourceIds, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {primaryId, secondaryResourceIds});
            if (secondaryResourceIds == null) throw new ArgumentNullException(nameof(secondaryResourceIds));

            var relationship = _targetedFields.Relationships.Single();

            if (secondaryResourceIds.Any())
            {
                using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);
                var primaryResource = collector.CreateForId<TResource, TId>(primaryId);

                await UpdateRelationshipAsync(relationship, primaryResource, secondaryResourceIds, collector, cancellationToken);

                await SaveChangesAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        public virtual async Task RemoveFromToManyRelationshipAsync(TResource primaryResource, ISet<IIdentifiable> secondaryResourceIds, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {primaryResource, secondaryResourceIds});
            if (secondaryResourceIds == null) throw new ArgumentNullException(nameof(secondaryResourceIds));

            var relationship = (HasManyAttribute)_targetedFields.Relationships.Single();

            var rightValue = relationship.GetValue(primaryResource);

            var rightResourceIds= TypeHelper.ExtractResources(rightValue).ToHashSet(IdentifiableComparer.Instance);
            rightResourceIds.ExceptWith(secondaryResourceIds);

            AssertIsNotClearingRequiredRelationship(relationship, primaryResource, rightResourceIds);

            using var collector = new PlaceholderResourceCollector(_resourceFactory, _dbContext);
            await UpdateRelationshipAsync(relationship, primaryResource, rightResourceIds, collector, cancellationToken);

            await SaveChangesAsync(cancellationToken);
        }

        protected async Task UpdateRelationshipAsync(RelationshipAttribute relationship, TResource leftResource,
            object valueToAssign, PlaceholderResourceCollector collector, CancellationToken cancellationToken)
        {
            var trackedValueToAssign = EnsureRelationshipValueToAssignIsTracked(valueToAssign, relationship.Property.PropertyType, collector);

            if (RequireLoadOfInverseRelationship(relationship, trackedValueToAssign))
            {
                var entityEntry = _dbContext.Entry(trackedValueToAssign);
                var inversePropertyName = relationship.InverseNavigationProperty.Name;

                await entityEntry.Reference(inversePropertyName).LoadAsync(cancellationToken);
            }

            relationship.SetValue(leftResource, trackedValueToAssign);
        }

        private object EnsureRelationshipValueToAssignIsTracked(object rightValue, Type relationshipPropertyType,
            PlaceholderResourceCollector collector)
        {
            if (rightValue == null)
            {
                return null;
            }

            var rightResources = TypeHelper.ExtractResources(rightValue);
            var rightResourcesTracked = rightResources.Select(collector.CaptureExisting).ToArray();

            return rightValue is IEnumerable
                ? (object) TypeHelper.CopyToTypedCollection(rightResourcesTracked, relationshipPropertyType)
                : rightResourcesTracked.Single();
        }

        private static bool RequireLoadOfInverseRelationship(RelationshipAttribute relationship, object trackedValueToAssign)
        {
            // See https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/502.
            return trackedValueToAssign != null && relationship.InverseNavigationProperty != null && IsOneToOneRelationship(relationship);
        }

        private static bool IsOneToOneRelationship(RelationshipAttribute relationship)
        {
            if (relationship is HasOneAttribute hasOneRelationship)
            {
                var elementType = TypeHelper.TryGetCollectionElementType(hasOneRelationship.InverseNavigationProperty.PropertyType);
                return elementType == null;
            }

            return false;
        }

        protected virtual async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new DataConcurrencyException(exception);
            }
            catch (DbUpdateException exception)
            {
                if (_dbContext.Database.CurrentTransaction != null)
                {
                    // The ResourceService calling us needs to run additional SQL queries after an aborted transaction,
                    // to determine error cause. This fails when a failed transaction is still in progress.
                    await _dbContext.Database.CurrentTransaction.RollbackAsync(cancellationToken);
                }

                throw new DataStoreUpdateException(exception);
            }
        }
    }

    /// <summary>
    /// Implements the foundational Repository layer in the JsonApiDotNetCore architecture that uses Entity Framework Core.
    /// </summary>
    public class EntityFrameworkCoreRepository<TResource> : EntityFrameworkCoreRepository<TResource, int>, IResourceRepository<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public EntityFrameworkCoreRepository(
            ITargetedFields targetedFields,
            IDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory)
            : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders, loggerFactory)
        {
        }
    }
}
