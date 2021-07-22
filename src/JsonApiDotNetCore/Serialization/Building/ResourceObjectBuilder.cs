using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Resources.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Serialization.Building
{
    /// <inheritdoc />
    [PublicAPI]
    public class ResourceObjectBuilder : IResourceObjectBuilder
    {
        private static readonly CollectionConverter CollectionConverter = new();

        private readonly ResourceObjectBuilderSettings _settings;
        protected IResourceContextProvider ResourceContextProvider { get; }

        public ResourceObjectBuilder(IResourceContextProvider resourceContextProvider, ResourceObjectBuilderSettings settings)
        {
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));
            ArgumentGuard.NotNull(settings, nameof(settings));

            ResourceContextProvider = resourceContextProvider;
            _settings = settings;
        }

        /// <inheritdoc />
        public virtual ResourceObject Build(IIdentifiable resource, IReadOnlyCollection<AttrAttribute> attributes = null,
            IReadOnlyCollection<RelationshipAttribute> relationships = null)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));

            ResourceContext resourceContext = ResourceContextProvider.GetResourceContext(resource.GetType());

            // populating the top-level "type" and "id" members.
            var resourceObject = new ResourceObject
            {
                Type = resourceContext.PublicName,
                Id = resource.StringId
            };

            // populating the top-level "attribute" member of a resource object. never include "id" as an attribute
            if (attributes != null)
            {
                AttrAttribute[] attributesWithoutId = attributes.Where(attr => attr.Property.Name != nameof(Identifiable.Id)).ToArray();

                if (attributesWithoutId.Any())
                {
                    ProcessAttributes(resource, attributesWithoutId, resourceObject);
                }
            }

            // populating the top-level "relationship" member of a resource object.
            if (relationships != null)
            {
                ProcessRelationships(resource, relationships, resourceObject);
            }

            return resourceObject;
        }

        /// <summary>
        /// Builds the <see cref="RelationshipEntry" /> entries of the "relationships objects". The default behavior is to just construct a resource linkage with
        /// the "data" field populated with "single" or "many" data. Depending on the requirements of the implementation (server or client serializer), this may
        /// be overridden.
        /// </summary>
        protected virtual RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable resource)
        {
            ArgumentGuard.NotNull(relationship, nameof(relationship));
            ArgumentGuard.NotNull(resource, nameof(resource));

            return new RelationshipEntry
            {
                Data = GetRelatedResourceLinkage(relationship, resource)
            };
        }

        /// <summary>
        /// Gets the value for the <see cref="ExposableData{T}.Data" /> property.
        /// </summary>
        protected object GetRelatedResourceLinkage(RelationshipAttribute relationship, IIdentifiable resource)
        {
            ArgumentGuard.NotNull(relationship, nameof(relationship));
            ArgumentGuard.NotNull(resource, nameof(resource));

            return relationship is HasOneAttribute hasOne
                ? GetRelatedResourceLinkageForHasOne(hasOne, resource)
                : GetRelatedResourceLinkageForHasMany((HasManyAttribute)relationship, resource);
        }

        /// <summary>
        /// Builds a <see cref="ResourceIdentifierObject" /> for a HasOne relationship.
        /// </summary>
        private ResourceIdentifierObject GetRelatedResourceLinkageForHasOne(HasOneAttribute relationship, IIdentifiable resource)
        {
            var relatedResource = (IIdentifiable)relationship.GetValue(resource);

            if (relatedResource != null)
            {
                return GetResourceIdentifier(relatedResource);
            }

            return null;
        }

        /// <summary>
        /// Builds the <see cref="ResourceIdentifierObject" />s for a HasMany relationship.
        /// </summary>
        private IList<ResourceIdentifierObject> GetRelatedResourceLinkageForHasMany(HasManyAttribute relationship, IIdentifiable resource)
        {
            object value = relationship.GetValue(resource);
            ICollection<IIdentifiable> relatedResources = CollectionConverter.ExtractResources(value);

            var manyData = new List<ResourceIdentifierObject>();

            if (relatedResources != null)
            {
                foreach (IIdentifiable relatedResource in relatedResources)
                {
                    manyData.Add(GetResourceIdentifier(relatedResource));
                }
            }

            return manyData;
        }

        /// <summary>
        /// Creates a <see cref="ResourceIdentifierObject" /> from <paramref name="resource" />.
        /// </summary>
        private ResourceIdentifierObject GetResourceIdentifier(IIdentifiable resource)
        {
            string resourceName = ResourceContextProvider.GetResourceContext(resource.GetType()).PublicName;

            return new ResourceIdentifierObject
            {
                Type = resourceName,
                Id = resource.StringId
            };
        }

        /// <summary>
        /// Puts the relationships of the resource into the resource object.
        /// </summary>
        private void ProcessRelationships(IIdentifiable resource, IEnumerable<RelationshipAttribute> relationships, ResourceObject ro)
        {
            foreach (RelationshipAttribute rel in relationships)
            {
                RelationshipEntry relData = GetRelationshipData(rel, resource);

                if (relData != null)
                {
                    (ro.Relationships ??= new Dictionary<string, RelationshipEntry>()).Add(rel.PublicName, relData);
                }
            }
        }

        /// <summary>
        /// Puts the attributes of the resource into the resource object.
        /// </summary>
        private void ProcessAttributes(IIdentifiable resource, IEnumerable<AttrAttribute> attributes, ResourceObject ro)
        {
            ro.Attributes = new Dictionary<string, object>();

            foreach (AttrAttribute attr in attributes)
            {
                object value = attr.GetValue(resource);

                if (_settings.SerializerNullValueHandling == NullValueHandling.Ignore && value == null)
                {
                    return;
                }

                if (_settings.SerializerDefaultValueHandling == DefaultValueHandling.Ignore &&
                    Equals(value, RuntimeTypeConverter.GetDefaultValue(attr.Property.PropertyType)))
                {
                    return;
                }

                ro.Attributes.Add(attr.PublicName, value);
            }
        }
    }
}
