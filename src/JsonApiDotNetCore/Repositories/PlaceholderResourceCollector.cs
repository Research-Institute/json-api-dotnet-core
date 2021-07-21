using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCore.Repositories
{
    /// <summary>
    /// Creates placeholder resource instances (with only their ID property set), which are added to the Entity Framework Core change tracker so they can be
    /// used in relationship updates without fetching the resource. On disposal, the created placeholders are detached, leaving the change tracker in a clean
    /// state for reuse.
    /// </summary>
    [PublicAPI]
    public sealed class PlaceholderResourceCollector : IDisposable
    {
        private readonly IResourceFactory _resourceFactory;
        private readonly DbContext _dbContext;
        private readonly List<object> _resources = new();

        public PlaceholderResourceCollector(IResourceFactory resourceFactory, DbContext dbContext)
        {
            ArgumentGuard.NotNull(resourceFactory, nameof(resourceFactory));
            ArgumentGuard.NotNull(dbContext, nameof(dbContext));

            _resourceFactory = resourceFactory;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Creates a new placeholder resource, assigns the specified ID, adds it to the change tracker in <see cref="EntityState.Unchanged" /> state and
        /// registers it for detachment.
        /// </summary>
        public TResource CreateForId<TResource, TId>(TId id)
            where TResource : IIdentifiable<TId>
        {
            var placeholderResource = _resourceFactory.CreateInstance<TResource>();
            placeholderResource.Id = id;

            return CaptureExisting(placeholderResource);
        }

        /// <summary>
        /// Takes an existing placeholder resource, adds it to the change tracker in <see cref="EntityState.Unchanged" /> state and registers it for detachment.
        /// </summary>
        public TResource CaptureExisting<TResource>(TResource placeholderResource)
            where TResource : IIdentifiable
        {
            var resourceTracked = (TResource)_dbContext.GetTrackedOrAttach(placeholderResource);

            if (ReferenceEquals(resourceTracked, placeholderResource))
            {
                _resources.Add(resourceTracked);
            }

            return resourceTracked;
        }

        /// <summary>
        /// Detaches the collected placeholder resources from the change tracker.
        /// </summary>
        public void Dispose()
        {
            Detach(_resources);

            _resources.Clear();
        }

        private void Detach(IEnumerable<object> resources)
        {
            foreach (object resource in resources)
            {
                try
                {
                    _dbContext.Entry(resource).State = EntityState.Detached;
                }
                catch (InvalidOperationException)
                {
                    // If SaveChanges() threw due to a foreign key constraint violation, its exception is rethrown here.
                    // We swallow this exception, to allow the originating error to propagate.
                }
            }
        }
    }
}
