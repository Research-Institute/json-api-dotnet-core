using System.Collections.Generic;
using JetBrains.Annotations;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JsonApiDotNetCore.Configuration
{
    /// <inheritdoc />
    [PublicAPI]
    public class InverseNavigationResolver : IInverseNavigationResolver
    {
        private readonly IResourceContextProvider _resourceContextProvider;
        private readonly IEnumerable<IDbContextResolver> _dbContextResolvers;

        public InverseNavigationResolver(IResourceContextProvider resourceContextProvider, IEnumerable<IDbContextResolver> dbContextResolvers)
        {
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));
            ArgumentGuard.NotNull(dbContextResolvers, nameof(dbContextResolvers));

            _resourceContextProvider = resourceContextProvider;
            _dbContextResolvers = dbContextResolvers;
        }

        /// <inheritdoc />
        public void Resolve()
        {
            foreach (IDbContextResolver dbContextResolver in _dbContextResolvers)
            {
                DbContext dbContext = dbContextResolver.GetContext();
                Resolve(dbContext);
            }
        }

        private void Resolve(DbContext dbContext)
        {
            foreach (ResourceContext resourceContext in _resourceContextProvider.GetResourceContexts())
            {
                IEntityType entityType = dbContext.Model.FindEntityType(resourceContext.ResourceType);

                if (entityType != null)
                {
                    ResolveRelationships(resourceContext.Relationships, entityType);
                }
            }
        }

        private void ResolveRelationships(IReadOnlyCollection<RelationshipAttribute> relationships, IEntityType entityType)
        {
            foreach (RelationshipAttribute relationship in relationships)
            {
                if (!(relationship is HasManyThroughAttribute))
                {
                    INavigation inverseNavigation = entityType.FindNavigation(relationship.Property.Name)?.Inverse;
                    relationship.InverseNavigationProperty = inverseNavigation?.PropertyInfo;
                }
            }
        }
    }
}
