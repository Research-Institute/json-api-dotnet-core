using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Serialization.Client.Internal
{
    /// <summary>
    /// Client serializer implementation of <see cref="BaseSerializer" />.
    /// </summary>
    [PublicAPI]
    public class RequestSerializer : BaseSerializer, IRequestSerializer
    {
        private readonly IResourceGraph _resourceGraph;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new();
        private Type _currentTargetedResource;

        /// <inheritdoc />
        public IReadOnlyCollection<AttrAttribute> AttributesToSerialize { get; set; }

        /// <inheritdoc />
        public IReadOnlyCollection<RelationshipAttribute> RelationshipsToSerialize { get; set; }

        public RequestSerializer(IResourceGraph resourceGraph, IResourceObjectBuilder resourceObjectBuilder)
            : base(resourceObjectBuilder)
        {
            ArgumentGuard.NotNull(resourceGraph, nameof(resourceGraph));

            _resourceGraph = resourceGraph;
        }

        /// <inheritdoc />
        public string Serialize(IIdentifiable resource)
        {
            if (resource == null)
            {
                Document empty = Build((IIdentifiable)null, Array.Empty<AttrAttribute>(), Array.Empty<RelationshipAttribute>());
                return SerializeObject(empty, _jsonSerializerSettings);
            }

            _currentTargetedResource = resource.GetType();
            Document document = Build(resource, GetAttributesToSerialize(resource), RelationshipsToSerialize);
            _currentTargetedResource = null;

            return SerializeObject(document, _jsonSerializerSettings);
        }

        /// <inheritdoc />
        public string Serialize(IReadOnlyCollection<IIdentifiable> resources)
        {
            ArgumentGuard.NotNull(resources, nameof(resources));

            IIdentifiable firstResource = resources.FirstOrDefault();

            Document document;

            if (firstResource == null)
            {
                document = Build(resources, Array.Empty<AttrAttribute>(), Array.Empty<RelationshipAttribute>());
            }
            else
            {
                _currentTargetedResource = firstResource.GetType();
                IReadOnlyCollection<AttrAttribute> attributes = GetAttributesToSerialize(firstResource);

                document = Build(resources, attributes, RelationshipsToSerialize);
                _currentTargetedResource = null;
            }

            return SerializeObject(document, _jsonSerializerSettings);
        }

        /// <summary>
        /// By default, the client serializer includes all attributes in the result, unless a list of allowed attributes was supplied using the
        /// <see cref="AttributesToSerialize" /> method. For any related resources, attributes are never exposed.
        /// </summary>
        private IReadOnlyCollection<AttrAttribute> GetAttributesToSerialize(IIdentifiable resource)
        {
            Type currentResourceType = resource.GetType();

            if (_currentTargetedResource != currentResourceType)
            {
                // We're dealing with a relationship that is being serialized, for which
                // we never want to include any attributes in the request body.
                return new List<AttrAttribute>();
            }

            if (AttributesToSerialize == null)
            {
                return _resourceGraph.GetAttributes(currentResourceType);
            }

            return AttributesToSerialize;
        }
    }
}
