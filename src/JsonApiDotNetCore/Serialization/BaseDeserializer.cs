using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Resources.Internal;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonApiDotNetCore.Serialization
{
    /// <summary>
    /// Abstract base class for deserialization. Deserializes JSON content into <see cref="Objects.Document" />s and constructs instances of the resource(s)
    /// in the document body.
    /// </summary>
    [PublicAPI]
    public abstract class BaseDeserializer
    {
        private protected static readonly CollectionConverter CollectionConverter = new();

        protected IResourceContextProvider ResourceContextProvider { get; }
        protected IResourceFactory ResourceFactory { get; }
        protected Document Document { get; set; }

        protected int? AtomicOperationIndex { get; set; }

        protected BaseDeserializer(IResourceContextProvider resourceContextProvider, IResourceFactory resourceFactory)
        {
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));
            ArgumentGuard.NotNull(resourceFactory, nameof(resourceFactory));

            ResourceContextProvider = resourceContextProvider;
            ResourceFactory = resourceFactory;
        }

        /// <summary>
        /// This method is called each time a <paramref name="resource" /> is constructed from the serialized content, which is used to do additional processing
        /// depending on the type of deserializer.
        /// </summary>
        /// <remarks>
        /// See the implementation of this method in <see cref="ResponseDeserializer" /> and <see cref="RequestDeserializer" /> for examples.
        /// </remarks>
        /// <param name="resource">
        /// The resource that was constructed from the document's body.
        /// </param>
        /// <param name="field">
        /// The metadata for the exposed field.
        /// </param>
        /// <param name="data">
        /// Relationship data for <paramref name="resource" />. Is null when <paramref name="field" /> is not a <see cref="RelationshipAttribute" />.
        /// </param>
        protected abstract void AfterProcessField(IIdentifiable resource, ResourceFieldAttribute field, RelationshipEntry data = null);

        protected object DeserializeBody(string body)
        {
            ArgumentGuard.NotNullNorEmpty(body, nameof(body));

            JToken bodyJToken = LoadJToken(body);
            Document = bodyJToken.ToObject<Document>();

            if (Document != null)
            {
                if (Document.IsManyData)
                {
                    return Document.ManyData.Select(ParseResourceObject).ToHashSet(IdentifiableComparer.Instance);
                }

                if (Document.SingleData != null)
                {
                    return ParseResourceObject(Document.SingleData);
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the attributes on a parsed resource.
        /// </summary>
        /// <param name="resource">
        /// The parsed resource.
        /// </param>
        /// <param name="attributeValues">
        /// Attributes and their values, as in the serialized content.
        /// </param>
        /// <param name="attributes">
        /// Exposed attributes for <paramref name="resource" />.
        /// </param>
        protected IIdentifiable SetAttributes(IIdentifiable resource, IDictionary<string, object> attributeValues,
            IReadOnlyCollection<AttrAttribute> attributes)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));
            ArgumentGuard.NotNull(attributes, nameof(attributes));

            if (attributeValues.IsNullOrEmpty())
            {
                return resource;
            }

            foreach (AttrAttribute attr in attributes)
            {
                if (attributeValues.TryGetValue(attr.PublicName, out object newValue))
                {
                    if (attr.Property.SetMethod == null)
                    {
                        throw new JsonApiSerializationException("Attribute is read-only.", $"Attribute '{attr.PublicName}' is read-only.",
                            atomicOperationIndex: AtomicOperationIndex);
                    }

                    object convertedValue = ConvertAttrValue(newValue, attr.Property.PropertyType);
                    attr.SetValue(resource, convertedValue);
                    AfterProcessField(resource, attr);
                }
            }

            return resource;
        }

        /// <summary>
        /// Sets the relationships on a parsed resource.
        /// </summary>
        /// <param name="resource">
        /// The parsed resource.
        /// </param>
        /// <param name="relationshipValues">
        /// Relationships and their values, as in the serialized content.
        /// </param>
        /// <param name="relationshipAttributes">
        /// Exposed relationships for <paramref name="resource" />.
        /// </param>
        protected virtual IIdentifiable SetRelationships(IIdentifiable resource, IDictionary<string, RelationshipEntry> relationshipValues,
            IReadOnlyCollection<RelationshipAttribute> relationshipAttributes)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));
            ArgumentGuard.NotNull(relationshipAttributes, nameof(relationshipAttributes));

            if (relationshipValues.IsNullOrEmpty())
            {
                return resource;
            }

            foreach (RelationshipAttribute attr in relationshipAttributes)
            {
                bool relationshipIsProvided = relationshipValues.TryGetValue(attr.PublicName, out RelationshipEntry relationshipData);

                if (!relationshipIsProvided || !relationshipData.IsPopulated)
                {
                    continue;
                }

                if (attr is HasOneAttribute hasOneAttribute)
                {
                    SetHasOneRelationship(resource, hasOneAttribute, relationshipData);
                }
                else if (attr is HasManyAttribute hasManyAttribute)
                {
                    SetHasManyRelationship(resource, hasManyAttribute, relationshipData);
                }
            }

            return resource;
        }

#pragma warning disable AV1130 // Return type in method signature should be a collection interface instead of a concrete type
        protected JToken LoadJToken(string body)
#pragma warning restore AV1130 // Return type in method signature should be a collection interface instead of a concrete type
        {
            using JsonReader jsonReader = new JsonTextReader(new StringReader(body))
            {
                // https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/509
                DateParseHandling = DateParseHandling.None
            };

            return JToken.Load(jsonReader);
        }

        /// <summary>
        /// Creates an instance of the referenced type in <paramref name="data" /> and sets its attributes and relationships.
        /// </summary>
        /// <returns>
        /// The parsed resource.
        /// </returns>
        protected IIdentifiable ParseResourceObject(ResourceObject data)
        {
            AssertHasType(data, null);

            if (AtomicOperationIndex == null)
            {
                AssertHasNoLid(data);
            }

            ResourceContext resourceContext = GetExistingResourceContext(data.Type);
            IIdentifiable resource = ResourceFactory.CreateInstance(resourceContext.ResourceType);

            resource = SetAttributes(resource, data.Attributes, resourceContext.Attributes);
            resource = SetRelationships(resource, data.Relationships, resourceContext.Relationships);

            if (data.Id != null)
            {
                resource.StringId = data.Id;
            }

            resource.LocalId = data.Lid;

            return resource;
        }

        protected ResourceContext GetExistingResourceContext(string publicName)
        {
            ResourceContext resourceContext = ResourceContextProvider.GetResourceContext(publicName);

            if (resourceContext == null)
            {
                throw new JsonApiSerializationException("Request body includes unknown resource type.", $"Resource type '{publicName}' does not exist.",
                    atomicOperationIndex: AtomicOperationIndex);
            }

            return resourceContext;
        }

        /// <summary>
        /// Sets a HasOne relationship on a parsed resource.
        /// </summary>
        private void SetHasOneRelationship(IIdentifiable resource, HasOneAttribute hasOneRelationship, RelationshipEntry relationshipData)
        {
            if (relationshipData.ManyData != null)
            {
                throw new JsonApiSerializationException("Expected single data element for to-one relationship.",
                    $"Expected single data element for '{hasOneRelationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
            }

            IIdentifiable rightResource = CreateRightResource(hasOneRelationship, relationshipData.SingleData);
            hasOneRelationship.SetValue(resource, rightResource);

            // depending on if this base parser is used client-side or server-side,
            // different additional processing per field needs to be executed.
            AfterProcessField(resource, hasOneRelationship, relationshipData);
        }

        /// <summary>
        /// Sets a HasMany relationship.
        /// </summary>
        private void SetHasManyRelationship(IIdentifiable resource, HasManyAttribute hasManyRelationship, RelationshipEntry relationshipData)
        {
            if (relationshipData.ManyData == null)
            {
                throw new JsonApiSerializationException("Expected data[] element for to-many relationship.",
                    $"Expected data[] element for '{hasManyRelationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
            }

            HashSet<IIdentifiable> rightResources = relationshipData.ManyData.Select(rio => CreateRightResource(hasManyRelationship, rio))
                .ToHashSet(IdentifiableComparer.Instance);

            IEnumerable convertedCollection = CollectionConverter.CopyToTypedCollection(rightResources, hasManyRelationship.Property.PropertyType);
            hasManyRelationship.SetValue(resource, convertedCollection);

            AfterProcessField(resource, hasManyRelationship, relationshipData);
        }

        private IIdentifiable CreateRightResource(RelationshipAttribute relationship, ResourceIdentifierObject resourceIdentifierObject)
        {
            if (resourceIdentifierObject != null)
            {
                AssertHasType(resourceIdentifierObject, relationship);
                AssertHasIdOrLid(resourceIdentifierObject, relationship);

                ResourceContext rightResourceContext = GetExistingResourceContext(resourceIdentifierObject.Type);
                AssertRightTypeIsCompatible(rightResourceContext, relationship);

                IIdentifiable rightInstance = ResourceFactory.CreateInstance(rightResourceContext.ResourceType);
                rightInstance.StringId = resourceIdentifierObject.Id;
                rightInstance.LocalId = resourceIdentifierObject.Lid;

                return rightInstance;
            }

            return null;
        }

        [AssertionMethod]
        private void AssertHasType(ResourceIdentifierObject resourceIdentifierObject, RelationshipAttribute relationship)
        {
            if (resourceIdentifierObject.Type == null)
            {
                string details = relationship != null
                    ? $"Expected 'type' element in '{relationship.PublicName}' relationship."
                    : "Expected 'type' element in 'data' element.";

                throw new JsonApiSerializationException("Request body must include 'type' element.", details, atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private void AssertHasIdOrLid(ResourceIdentifierObject resourceIdentifierObject, RelationshipAttribute relationship)
        {
            if (AtomicOperationIndex != null)
            {
                bool hasNone = resourceIdentifierObject.Id == null && resourceIdentifierObject.Lid == null;
                bool hasBoth = resourceIdentifierObject.Id != null && resourceIdentifierObject.Lid != null;

                if (hasNone || hasBoth)
                {
                    throw new JsonApiSerializationException("Request body must include 'id' or 'lid' element.",
                        $"Expected 'id' or 'lid' element in '{relationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
                }
            }
            else
            {
                if (resourceIdentifierObject.Id == null)
                {
                    throw new JsonApiSerializationException("Request body must include 'id' element.",
                        $"Expected 'id' element in '{relationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
                }

                AssertHasNoLid(resourceIdentifierObject);
            }
        }

        [AssertionMethod]
        private void AssertHasNoLid(ResourceIdentifierObject resourceIdentifierObject)
        {
            if (resourceIdentifierObject.Lid != null)
            {
                throw new JsonApiSerializationException("Local IDs cannot be used at this endpoint.", null, atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private void AssertRightTypeIsCompatible(ResourceContext rightResourceContext, RelationshipAttribute relationship)
        {
            if (!relationship.RightType.IsAssignableFrom(rightResourceContext.ResourceType))
            {
                throw new JsonApiSerializationException("Relationship contains incompatible resource type.",
                    $"Relationship '{relationship.PublicName}' contains incompatible resource type '{rightResourceContext.PublicName}'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private object ConvertAttrValue(object newValue, Type targetType)
        {
            if (newValue is JContainer jObject)
            {
                // the attribute value is a complex type that needs additional deserialization
                return DeserializeComplexType(jObject, targetType);
            }

            // the attribute value is a native C# type.
            object convertedValue = RuntimeTypeConverter.ConvertType(newValue, targetType);
            return convertedValue;
        }

        private object DeserializeComplexType(JContainer obj, Type targetType)
        {
            return obj.ToObject(targetType);
        }
    }
}
