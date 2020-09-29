using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonApiDotNetCore.Serialization
{
    /// <summary>
    /// Abstract base class for deserialization. Deserializes JSON content into <see cref="Objects.Document"/>s
    /// and constructs instances of the resource(s) in the document body.
    /// </summary>
    public abstract class BaseDeserializer
    {
        protected IResourceContextProvider ResourceContextProvider { get; }
        protected IResourceFactory ResourceFactory{ get; }
        protected Document Document { get; set; }

        protected BaseDeserializer(IResourceContextProvider resourceContextProvider, IResourceFactory resourceFactory)
        {
            ResourceContextProvider = resourceContextProvider ?? throw new ArgumentNullException(nameof(resourceContextProvider));
            ResourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
        }

        /// <summary>
        /// This method is called each time a <paramref name="resource"/> is constructed
        /// from the serialized content, which is used to do additional processing
        /// depending on the type of deserializer.
        /// </summary>
        /// <remarks>
        /// See the implementation of this method in <see cref="ResponseDeserializer"/>
        /// and <see cref="RequestDeserializer"/> for examples.
        /// </remarks>
        /// <param name="resource">The resource that was constructed from the document's body.</param>
        /// <param name="field">The metadata for the exposed field.</param>
        /// <param name="data">Relationship data for <paramref name="resource"/>. Is null when <paramref name="field"/> is not a <see cref="RelationshipAttribute"/>.</param>
        protected abstract void AfterProcessField(IIdentifiable resource, ResourceFieldAttribute field, RelationshipEntry data = null);

        protected object DeserializeBody(string body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            var bodyJToken = LoadJToken(body);
            Document = bodyJToken.ToObject<Document>();
            if (Document.IsManyData)
            {
                if (Document.ManyData.Count == 0)
                    return Array.Empty<IIdentifiable>();

                return Document.ManyData.Select(ParseResourceObject).ToArray();
            }

            if (Document.SingleData == null) return null;
            return ParseResourceObject(Document.SingleData);
        }

        /// <summary>
        /// Sets the attributes on a parsed resource.
        /// </summary>
        /// <param name="resource">The parsed resource.</param>
        /// <param name="attributeValues">Attributes and their values, as in the serialized content.</param>
        /// <param name="attributes">Exposed attributes for <paramref name="resource"/>.</param>
        protected virtual IIdentifiable SetAttributes(IIdentifiable resource, IDictionary<string, object> attributeValues, IReadOnlyCollection<AttrAttribute> attributes)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));

            if (attributeValues == null || attributeValues.Count == 0)
                return resource;

            foreach (var attr in attributes)
            {
                if (attributeValues.TryGetValue(attr.PublicName, out object newValue))
                {
                    var convertedValue = ConvertAttrValue(newValue, attr.Property.PropertyType);
                    attr.SetValue(resource, convertedValue);
                    AfterProcessField(resource, attr);
                }
            }

            return resource;
        }

        /// <summary>
        /// Sets the relationships on a parsed resource.
        /// </summary>
        /// <param name="resource">The parsed resource.</param>
        /// <param name="relationshipValues">Relationships and their values, as in the serialized content.</param>
        /// <param name="relationshipAttributes">Exposed relationships for <paramref name="resource"/>.</param>
        protected virtual IIdentifiable SetRelationships(IIdentifiable resource, IDictionary<string, RelationshipEntry> relationshipValues, IReadOnlyCollection<RelationshipAttribute> relationshipAttributes)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (relationshipAttributes == null) throw new ArgumentNullException(nameof(relationshipAttributes));

            if (relationshipValues == null || relationshipValues.Count == 0)
                return resource;

            var resourceProperties = resource.GetType().GetProperties();
            foreach (var attr in relationshipAttributes)
            {
                var relationshipIsProvided = relationshipValues.TryGetValue(attr.PublicName, out RelationshipEntry relationshipData);
                if (!relationshipIsProvided || !relationshipData.IsPopulated)
                    continue;

                if (attr is HasOneAttribute hasOneAttribute)
                    SetHasOneRelationship(resource, resourceProperties, hasOneAttribute, relationshipData);
                else
                    SetHasManyRelationship(resource, (HasManyAttribute)attr, relationshipData);
            }
            return resource;
        }

        private JToken LoadJToken(string body)
        {
            JToken jToken;
            using (JsonReader jsonReader = new JsonTextReader(new StringReader(body)))
            {
                // https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/509
                jsonReader.DateParseHandling = DateParseHandling.None;
                jToken = JToken.Load(jsonReader);
            }
            return jToken;
        }

        /// <summary>
        /// Creates an instance of the referenced type in <paramref name="data"/>
        /// and sets its attributes and relationships.
        /// </summary>
        /// <returns>The parsed resource.</returns>
        private IIdentifiable ParseResourceObject(ResourceObject data)
        {
            var resourceContext = ResourceContextProvider.GetResourceContext(data.Type);
            if (resourceContext == null)
            {
                throw new InvalidRequestBodyException("Payload includes unknown resource type.",
                    $"The resource '{data.Type}' is not registered on the resource graph. " +
                    "If you are using Entity Framework Core, make sure the DbSet matches the expected resource name. " +
                    "If you have manually registered the resource, check that the call to Add correctly sets the public name.", null);
            }

            var resource = (IIdentifiable)ResourceFactory.CreateInstance(resourceContext.ResourceType);

            resource = SetAttributes(resource, data.Attributes, resourceContext.Attributes);
            resource = SetRelationships(resource, data.Relationships, resourceContext.Relationships);

            if (data.Id != null)
                resource.StringId = data.Id;

            return resource;
        }

        /// <summary>
        /// Sets a HasOne relationship on a parsed resource. If present, also
        /// populates the foreign key.
        /// </summary>
        private void SetHasOneRelationship(IIdentifiable resource,
            PropertyInfo[] resourceProperties,
            HasOneAttribute attr,
            RelationshipEntry relationshipData)
        {
            var rio = (ResourceIdentifierObject)relationshipData.Data;
            var relatedId = rio?.Id;

            var relationshipType = relationshipData.SingleData == null
                ? attr.RightType
                : ResourceContextProvider.GetResourceContext(relationshipData.SingleData.Type).ResourceType;

            // this does not make sense in the following case: if we're setting the dependent of a one-to-one relationship, IdentifiablePropertyName should be null.
            var foreignKeyProperty = resourceProperties.FirstOrDefault(p => p.Name == attr.IdentifiablePropertyName);

            if (foreignKeyProperty != null)
                // there is a FK from the current resource pointing to the related object,
                // i.e. we're populating the relationship from the dependent side.
                SetForeignKey(resource, foreignKeyProperty, attr, relatedId, relationshipType);

            SetNavigation(resource, attr, relatedId, relationshipType);

            // depending on if this base parser is used client-side or server-side,
            // different additional processing per field needs to be executed.
            AfterProcessField(resource, attr, relationshipData);
        }

        /// <summary>
        /// Sets the dependent side of a HasOne relationship, which means that a
        /// foreign key also will be populated.
        /// </summary>
        private void SetForeignKey(IIdentifiable resource, PropertyInfo foreignKey, HasOneAttribute attr, string id,
            Type relationshipType)
        {
            bool foreignKeyPropertyIsNullableType = Nullable.GetUnderlyingType(foreignKey.PropertyType) != null
                || foreignKey.PropertyType == typeof(string);
            if (id == null && !foreignKeyPropertyIsNullableType)
            {
                // this happens when a non-optional relationship is deliberately set to null.
                // For a server deserializer, it should be mapped to a BadRequest HTTP error code.
                throw new FormatException($"Cannot set required relationship identifier '{attr.IdentifiablePropertyName}' to null because it is a non-nullable type.");
            }

            var typedId = TypeHelper.ConvertStringIdToTypedId(relationshipType, id, ResourceFactory);
            foreignKey.SetValue(resource, typedId);
        }

        /// <summary>
        /// Sets the principal side of a HasOne relationship, which means no
        /// foreign key is involved.
        /// </summary>
        private void SetNavigation(IIdentifiable resource, HasOneAttribute attr, string relatedId,
            Type relationshipType)
        {
            if (relatedId == null)
            {
                attr.SetValue(resource, null, ResourceFactory);
            }
            else
            {
                var relatedInstance = (IIdentifiable)ResourceFactory.CreateInstance(relationshipType);
                relatedInstance.StringId = relatedId;
                attr.SetValue(resource, relatedInstance, ResourceFactory);
            }
        }

        /// <summary>
        /// Sets a HasMany relationship.
        /// </summary>
        private void SetHasManyRelationship(
            IIdentifiable resource,
            HasManyAttribute attr,
            RelationshipEntry relationshipData)
        {
            if (relationshipData.Data != null)
            {   // if the relationship is set to null, no need to set the navigation property to null: this is the default value.
                var relatedResources = relationshipData.ManyData.Select(rio =>
                {
                    var relationshipType = ResourceContextProvider.GetResourceContext(rio.Type).ResourceType;
                    var relatedInstance = (IIdentifiable)ResourceFactory.CreateInstance(relationshipType);
                    relatedInstance.StringId = rio.Id;
                    
                    return relatedInstance;
                });

                var convertedCollection = TypeHelper.CopyToTypedCollection(relatedResources, attr.Property.PropertyType);
                attr.SetValue(resource, convertedCollection, ResourceFactory);
            }

            AfterProcessField(resource, attr, relationshipData);
        }

        private object ConvertAttrValue(object newValue, Type targetType)
        {
            if (newValue is JContainer jObject)
                // the attribute value is a complex type that needs additional deserialization
                return DeserializeComplexType(jObject, targetType);

            // the attribute value is a native C# type.
            var convertedValue = TypeHelper.ConvertType(newValue, targetType);
            return convertedValue;
        }

        private object DeserializeComplexType(JContainer obj, Type targetType)
        {
            return obj.ToObject(targetType);
        }
    }
}
