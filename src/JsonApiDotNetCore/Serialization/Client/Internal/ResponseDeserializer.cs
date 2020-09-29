using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Serialization.Client.Internal
{
    /// <summary>
    /// Client deserializer implementation of the <see cref="BaseDeserializer"/>.
    /// </summary>
    public class ResponseDeserializer : BaseDeserializer, IResponseDeserializer
    {
        public ResponseDeserializer(IResourceContextProvider resourceContextProvider, IResourceFactory resourceFactory) : base(resourceContextProvider, resourceFactory) { }

        /// <inheritdoc />
        public SingleResponse<TResource> DeserializeSingle<TResource>(string body) where TResource : class, IIdentifiable
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            var resource = DeserializeBody(body);
            return new SingleResponse<TResource>
            {
                Links = Document.Links,
                Meta = Document.Meta,
                Data = (TResource) resource,
                JsonApi = null,
                Errors = null
            };
        }

        /// <inheritdoc />
        public ManyResponse<TResource> DeserializeMany<TResource>(string body) where TResource : class, IIdentifiable
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            var resources = DeserializeBody(body);
            return new ManyResponse<TResource>
            {
                Links = Document.Links,
                Meta = Document.Meta,
                Data = ((ICollection<IIdentifiable>) resources)?.Cast<TResource>().ToArray(),
                JsonApi = null,
                Errors = null
            };
        }

        /// <summary>
        /// Additional processing required for client deserialization, responsible
        /// for parsing the <see cref="Document.Included"/> property. When a relationship value is parsed,
        /// it goes through the included list to set its attributes and relationships.
        /// </summary>
        /// <param name="resource">The resource that was constructed from the document's body.</param>
        /// <param name="field">The metadata for the exposed field.</param>
        /// <param name="data">Relationship data for <paramref name="resource"/>. Is null when <paramref name="field"/> is not a <see cref="RelationshipAttribute"/>.</param>
        protected override void AfterProcessField(IIdentifiable resource, ResourceFieldAttribute field, RelationshipEntry data = null)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (field == null) throw new ArgumentNullException(nameof(field));

            // Client deserializers do not need additional processing for attributes.
            if (field is AttrAttribute)
                return;

            // if the included property is empty or absent, there is no additional data to be parsed.
            if (Document.Included == null || Document.Included.Count == 0)
                return;

            if (field is HasOneAttribute hasOneAttr)
            {
                // add attributes and relationships of a parsed HasOne relationship
                var rio = data.SingleData;
                hasOneAttr.SetValue(resource, rio == null ? null : ParseIncludedRelationship(rio), ResourceFactory);
            }
            else if (field is HasManyAttribute hasManyAttr)
            {  // add attributes and relationships of a parsed HasMany relationship
                var items = data.ManyData.Select(rio => ParseIncludedRelationship(rio));
                var values = TypeHelper.CopyToTypedCollection(items, hasManyAttr.Property.PropertyType);
                hasManyAttr.SetValue(resource, values, ResourceFactory);
            }
        }

        /// <summary>
        /// Searches for and parses the included relationship.
        /// </summary>
        private IIdentifiable ParseIncludedRelationship(ResourceIdentifierObject relatedResourceIdentifier)
        {
            var relatedResourceContext = ResourceContextProvider.GetResourceContext(relatedResourceIdentifier.Type);

            if (relatedResourceContext == null)
            {
                throw new InvalidOperationException($"Included type '{relatedResourceIdentifier.Type}' is not a registered json:api resource.");
            }
            
            var relatedInstance = (IIdentifiable)ResourceFactory.CreateInstance(relatedResourceContext.ResourceType);
            relatedInstance.StringId = relatedResourceIdentifier.Id;

            var includedResource = GetLinkedResource(relatedResourceIdentifier);

            if (includedResource != null)
            {
                SetAttributes(relatedInstance, includedResource.Attributes, relatedResourceContext.Attributes);
                SetRelationships(relatedInstance, includedResource.Relationships, relatedResourceContext.Relationships);
            }
            
            return relatedInstance;
        }

        private ResourceObject GetLinkedResource(ResourceIdentifierObject relatedResourceIdentifier)
        {
            try
            {
                return Document.Included.SingleOrDefault(r => r.Type == relatedResourceIdentifier.Type && r.Id == relatedResourceIdentifier.Id);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException("A compound document MUST NOT include more than one resource object for each type and ID pair."
                        + $"The duplicate pair was '{relatedResourceIdentifier.Type}, {relatedResourceIdentifier.Id}'", e);
            }
        }
    }
}
