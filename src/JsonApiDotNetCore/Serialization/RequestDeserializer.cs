using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Humanizer;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Resources.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace JsonApiDotNetCore.Serialization
{
    /// <summary>
    /// Server deserializer implementation of the <see cref="BaseDeserializer" />.
    /// </summary>
    [PublicAPI]
    public class RequestDeserializer : BaseDeserializer, IJsonApiDeserializer
    {
        private readonly ITargetedFields _targetedFields;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IJsonApiRequest _request;
        private readonly IJsonApiOptions _options;
        private readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;

        public RequestDeserializer(IResourceContextProvider resourceContextProvider, IResourceFactory resourceFactory, ITargetedFields targetedFields,
            IHttpContextAccessor httpContextAccessor, IJsonApiRequest request, IJsonApiOptions options)
            : base(resourceContextProvider, resourceFactory)
        {
            ArgumentGuard.NotNull(targetedFields, nameof(targetedFields));
            ArgumentGuard.NotNull(httpContextAccessor, nameof(httpContextAccessor));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(options, nameof(options));

            _targetedFields = targetedFields;
            _httpContextAccessor = httpContextAccessor;
            _request = request;
            _options = options;

#pragma warning disable 612 // Method is obsolete
            _resourceDefinitionAccessor = resourceFactory.GetResourceDefinitionAccessor();
#pragma warning restore 612
        }

        /// <inheritdoc />
        public object Deserialize(string body)
        {
            ArgumentGuard.NotNullNorEmpty(body, nameof(body));

            if (_request.Kind == EndpointKind.Relationship)
            {
                _targetedFields.Relationships.Add(_request.Relationship);
            }

            if (_request.Kind == EndpointKind.AtomicOperations)
            {
                return DeserializeOperationsDocument(body);
            }

            object instance = DeserializeBody(body);

            if (instance is IIdentifiable resource && _request.Kind != EndpointKind.Relationship)
            {
                _resourceDefinitionAccessor.OnDeserialize(resource);
            }

            AssertResourceIdIsNotTargeted(_targetedFields);

            return instance;
        }

        private object DeserializeOperationsDocument(string body)
        {
            JToken bodyToken = LoadJToken(body);
            var document = bodyToken.ToObject<AtomicOperationsDocument>();

            if ((document?.Operations).IsNullOrEmpty())
            {
                throw new JsonApiSerializationException("No operations found.", null);
            }

            if (document.Operations.Count > _options.MaximumOperationsPerRequest)
            {
                throw new JsonApiSerializationException("Request exceeds the maximum number of operations.",
                    $"The number of operations in this request ({document.Operations.Count}) is higher than {_options.MaximumOperationsPerRequest}.");
            }

            var operations = new List<OperationContainer>();
            AtomicOperationIndex = 0;

            foreach (AtomicOperationObject operation in document.Operations)
            {
                OperationContainer container = DeserializeOperation(operation);
                operations.Add(container);

                AtomicOperationIndex++;
            }

            return operations;
        }

        private OperationContainer DeserializeOperation(AtomicOperationObject operation)
        {
            _targetedFields.Attributes.Clear();
            _targetedFields.Relationships.Clear();

            AssertHasNoHref(operation);

            OperationKind kind = GetOperationKind(operation);

            switch (kind)
            {
                case OperationKind.CreateResource:
                case OperationKind.UpdateResource:
                {
                    return ParseForCreateOrUpdateResourceOperation(operation, kind);
                }
                case OperationKind.DeleteResource:
                {
                    return ParseForDeleteResourceOperation(operation, kind);
                }
            }

            bool requireToManyRelationship = kind == OperationKind.AddToRelationship || kind == OperationKind.RemoveFromRelationship;

            return ParseForRelationshipOperation(operation, kind, requireToManyRelationship);
        }

        [AssertionMethod]
        private void AssertHasNoHref(AtomicOperationObject operation)
        {
            if (operation.Href != null)
            {
                throw new JsonApiSerializationException("Usage of the 'href' element is not supported.", null, atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private OperationKind GetOperationKind(AtomicOperationObject operation)
        {
            switch (operation.Code)
            {
                case AtomicOperationCode.Add:
                {
                    if (operation.Ref is { Relationship: null })
                    {
                        throw new JsonApiSerializationException("The 'ref.relationship' element is required.", null,
                            atomicOperationIndex: AtomicOperationIndex);
                    }

                    return operation.Ref == null ? OperationKind.CreateResource : OperationKind.AddToRelationship;
                }
                case AtomicOperationCode.Update:
                {
                    return operation.Ref?.Relationship != null ? OperationKind.SetRelationship : OperationKind.UpdateResource;
                }
                case AtomicOperationCode.Remove:
                {
                    if (operation.Ref == null)
                    {
                        throw new JsonApiSerializationException("The 'ref' element is required.", null, atomicOperationIndex: AtomicOperationIndex);
                    }

                    return operation.Ref.Relationship != null ? OperationKind.RemoveFromRelationship : OperationKind.DeleteResource;
                }
            }

            throw new NotSupportedException($"Unknown operation code '{operation.Code}'.");
        }

        private OperationContainer ParseForCreateOrUpdateResourceOperation(AtomicOperationObject operation, OperationKind kind)
        {
            ResourceObject resourceObject = GetRequiredSingleDataForResourceOperation(operation);

            AssertElementHasType(resourceObject, "data");
            AssertElementHasIdOrLid(resourceObject, "data", kind != OperationKind.CreateResource);

            ResourceContext primaryResourceContext = GetExistingResourceContext(resourceObject.Type);

            AssertCompatibleId(resourceObject, primaryResourceContext.IdentityType);

            if (operation.Ref != null)
            {
                // For resource update, 'ref' is optional. But when specified, it must match with 'data'.

                AssertElementHasType(operation.Ref, "ref");
                AssertElementHasIdOrLid(operation.Ref, "ref", true);

                ResourceContext resourceContextInRef = GetExistingResourceContext(operation.Ref.Type);

                if (resourceContextInRef != primaryResourceContext)
                {
                    throw new JsonApiSerializationException("Resource type mismatch between 'ref.type' and 'data.type' element.",
                        $"Expected resource of type '{resourceContextInRef.PublicName}' in 'data.type', instead of '{primaryResourceContext.PublicName}'.",
                        atomicOperationIndex: AtomicOperationIndex);
                }

                AssertSameIdentityInRefData(operation, resourceObject);
            }

            var request = new JsonApiRequest
            {
                Kind = EndpointKind.AtomicOperations,
#pragma warning disable CS0618 // Type or member is obsolete
                BasePath = _request.BasePath,
#pragma warning restore CS0618 // Type or member is obsolete
                PrimaryResource = primaryResourceContext,
                OperationKind = kind
            };

            _request.CopyFrom(request);

            IIdentifiable primaryResource = ParseResourceObject(operation.SingleData);

            _resourceDefinitionAccessor.OnDeserialize(primaryResource);

            request.PrimaryId = primaryResource.StringId;
            _request.CopyFrom(request);

            var targetedFields = new TargetedFields
            {
                Attributes = _targetedFields.Attributes.ToHashSet(),
                Relationships = _targetedFields.Relationships.ToHashSet()
            };

            AssertResourceIdIsNotTargeted(targetedFields);

            return new OperationContainer(kind, primaryResource, targetedFields, request);
        }

        private ResourceObject GetRequiredSingleDataForResourceOperation(AtomicOperationObject operation)
        {
            if (operation.Data == null)
            {
                throw new JsonApiSerializationException("The 'data' element is required.", null, atomicOperationIndex: AtomicOperationIndex);
            }

            if (operation.SingleData == null)
            {
                throw new JsonApiSerializationException("Expected single data element for create/update resource operation.", null,
                    atomicOperationIndex: AtomicOperationIndex);
            }

            return operation.SingleData;
        }

        [AssertionMethod]
        private void AssertElementHasType(ResourceIdentifierObject resourceIdentifierObject, string elementPath)
        {
            if (resourceIdentifierObject.Type == null)
            {
                throw new JsonApiSerializationException($"The '{elementPath}.type' element is required.", null, atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private void AssertElementHasIdOrLid(ResourceIdentifierObject resourceIdentifierObject, string elementPath, bool isRequired)
        {
            bool hasNone = resourceIdentifierObject.Id == null && resourceIdentifierObject.Lid == null;
            bool hasBoth = resourceIdentifierObject.Id != null && resourceIdentifierObject.Lid != null;

            if (isRequired ? hasNone || hasBoth : hasBoth)
            {
                throw new JsonApiSerializationException($"The '{elementPath}.id' or '{elementPath}.lid' element is required.", null,
                    atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private void AssertCompatibleId(ResourceIdentifierObject resourceIdentifierObject, Type idType)
        {
            if (resourceIdentifierObject.Id != null)
            {
                try
                {
                    RuntimeTypeConverter.ConvertType(resourceIdentifierObject.Id, idType);
                }
                catch (FormatException exception)
                {
                    throw new JsonApiSerializationException(null, exception.Message, null, AtomicOperationIndex);
                }
            }
        }

        private void AssertSameIdentityInRefData(AtomicOperationObject operation, ResourceIdentifierObject resourceIdentifierObject)
        {
            if (operation.Ref.Id != null && resourceIdentifierObject.Id != null && resourceIdentifierObject.Id != operation.Ref.Id)
            {
                throw new JsonApiSerializationException("Resource ID mismatch between 'ref.id' and 'data.id' element.",
                    $"Expected resource with ID '{operation.Ref.Id}' in 'data.id', instead of '{resourceIdentifierObject.Id}'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }

            if (operation.Ref.Lid != null && resourceIdentifierObject.Lid != null && resourceIdentifierObject.Lid != operation.Ref.Lid)
            {
                throw new JsonApiSerializationException("Resource local ID mismatch between 'ref.lid' and 'data.lid' element.",
                    $"Expected resource with local ID '{operation.Ref.Lid}' in 'data.lid', instead of '{resourceIdentifierObject.Lid}'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }

            if (operation.Ref.Id != null && resourceIdentifierObject.Lid != null)
            {
                throw new JsonApiSerializationException("Resource identity mismatch between 'ref.id' and 'data.lid' element.",
                    $"Expected resource with ID '{operation.Ref.Id}' in 'data.id', instead of '{resourceIdentifierObject.Lid}' in 'data.lid'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }

            if (operation.Ref.Lid != null && resourceIdentifierObject.Id != null)
            {
                throw new JsonApiSerializationException("Resource identity mismatch between 'ref.lid' and 'data.id' element.",
                    $"Expected resource with local ID '{operation.Ref.Lid}' in 'data.lid', instead of '{resourceIdentifierObject.Id}' in 'data.id'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private OperationContainer ParseForDeleteResourceOperation(AtomicOperationObject operation, OperationKind kind)
        {
            AssertElementHasType(operation.Ref, "ref");
            AssertElementHasIdOrLid(operation.Ref, "ref", true);

            ResourceContext primaryResourceContext = GetExistingResourceContext(operation.Ref.Type);

            AssertCompatibleId(operation.Ref, primaryResourceContext.IdentityType);

            IIdentifiable primaryResource = ResourceFactory.CreateInstance(primaryResourceContext.ResourceType);
            primaryResource.StringId = operation.Ref.Id;
            primaryResource.LocalId = operation.Ref.Lid;

            var request = new JsonApiRequest
            {
                Kind = EndpointKind.AtomicOperations,
#pragma warning disable CS0618 // Type or member is obsolete
                BasePath = _request.BasePath,
#pragma warning restore CS0618 // Type or member is obsolete
                PrimaryId = primaryResource.StringId,
                PrimaryResource = primaryResourceContext,
                OperationKind = kind
            };

            return new OperationContainer(kind, primaryResource, new TargetedFields(), request);
        }

        private OperationContainer ParseForRelationshipOperation(AtomicOperationObject operation, OperationKind kind, bool requireToMany)
        {
            AssertElementHasType(operation.Ref, "ref");
            AssertElementHasIdOrLid(operation.Ref, "ref", true);

            ResourceContext primaryResourceContext = GetExistingResourceContext(operation.Ref.Type);

            AssertCompatibleId(operation.Ref, primaryResourceContext.IdentityType);

            IIdentifiable primaryResource = ResourceFactory.CreateInstance(primaryResourceContext.ResourceType);
            primaryResource.StringId = operation.Ref.Id;
            primaryResource.LocalId = operation.Ref.Lid;

            RelationshipAttribute relationship = GetExistingRelationship(operation.Ref, primaryResourceContext);

            if (requireToMany && relationship is HasOneAttribute)
            {
                throw new JsonApiSerializationException($"Only to-many relationships can be targeted in '{operation.Code.ToString().Camelize()}' operations.",
                    $"Relationship '{operation.Ref.Relationship}' must be a to-many relationship.", atomicOperationIndex: AtomicOperationIndex);
            }

            ResourceContext secondaryResourceContext = ResourceContextProvider.GetResourceContext(relationship.RightType);

            var request = new JsonApiRequest
            {
                Kind = EndpointKind.AtomicOperations,
#pragma warning disable CS0618 // Type or member is obsolete
                BasePath = _request.BasePath,
#pragma warning restore CS0618 // Type or member is obsolete
                PrimaryId = primaryResource.StringId,
                PrimaryResource = primaryResourceContext,
                SecondaryResource = secondaryResourceContext,
                Relationship = relationship,
                IsCollection = relationship is HasManyAttribute,
                OperationKind = kind
            };

            _request.CopyFrom(request);

            _targetedFields.Relationships.Add(relationship);

            ParseDataForRelationship(relationship, secondaryResourceContext, operation, primaryResource);

            var targetedFields = new TargetedFields
            {
                Attributes = _targetedFields.Attributes.ToHashSet(),
                Relationships = _targetedFields.Relationships.ToHashSet()
            };

            return new OperationContainer(kind, primaryResource, targetedFields, request);
        }

        private RelationshipAttribute GetExistingRelationship(AtomicReference reference, ResourceContext resourceContext)
        {
            RelationshipAttribute relationship = resourceContext.Relationships.FirstOrDefault(attribute => attribute.PublicName == reference.Relationship);

            if (relationship == null)
            {
                throw new JsonApiSerializationException("The referenced relationship does not exist.",
                    $"Resource of type '{reference.Type}' does not contain a relationship named '{reference.Relationship}'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }

            return relationship;
        }

        private void ParseDataForRelationship(RelationshipAttribute relationship, ResourceContext secondaryResourceContext, AtomicOperationObject operation,
            IIdentifiable primaryResource)
        {
            if (relationship is HasOneAttribute)
            {
                if (operation.ManyData != null)
                {
                    throw new JsonApiSerializationException("Expected single data element for to-one relationship.",
                        $"Expected single data element for '{relationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
                }

                if (operation.SingleData != null)
                {
                    ValidateSingleDataForRelationship(operation.SingleData, secondaryResourceContext, "data");

                    IIdentifiable secondaryResource = ParseResourceObject(operation.SingleData);
                    relationship.SetValue(primaryResource, secondaryResource);
                }
            }
            else if (relationship is HasManyAttribute)
            {
                if (operation.ManyData == null)
                {
                    throw new JsonApiSerializationException("Expected data[] element for to-many relationship.",
                        $"Expected data[] element for '{relationship.PublicName}' relationship.", atomicOperationIndex: AtomicOperationIndex);
                }

                var secondaryResources = new List<IIdentifiable>();

                foreach (ResourceObject resourceObject in operation.ManyData)
                {
                    ValidateSingleDataForRelationship(resourceObject, secondaryResourceContext, "data[]");

                    IIdentifiable secondaryResource = ParseResourceObject(resourceObject);
                    secondaryResources.Add(secondaryResource);
                }

                IEnumerable rightResources = CollectionConverter.CopyToTypedCollection(secondaryResources, relationship.Property.PropertyType);
                relationship.SetValue(primaryResource, rightResources);
            }
        }

        private void ValidateSingleDataForRelationship(ResourceObject dataResourceObject, ResourceContext resourceContext, string elementPath)
        {
            AssertElementHasType(dataResourceObject, elementPath);
            AssertElementHasIdOrLid(dataResourceObject, elementPath, true);

            ResourceContext resourceContextInData = GetExistingResourceContext(dataResourceObject.Type);

            AssertCompatibleType(resourceContextInData, resourceContext, elementPath);
            AssertCompatibleId(dataResourceObject, resourceContextInData.IdentityType);
        }

        private void AssertCompatibleType(ResourceContext resourceContextInData, ResourceContext resourceContextInRef, string elementPath)
        {
            if (!resourceContextInData.ResourceType.IsAssignableFrom(resourceContextInRef.ResourceType))
            {
                throw new JsonApiSerializationException($"Resource type mismatch between 'ref.relationship' and '{elementPath}.type' element.",
                    $"Expected resource of type '{resourceContextInRef.PublicName}' in '{elementPath}.type', instead of '{resourceContextInData.PublicName}'.",
                    atomicOperationIndex: AtomicOperationIndex);
            }
        }

        private void AssertResourceIdIsNotTargeted(ITargetedFields targetedFields)
        {
            if (!_request.IsReadOnly && targetedFields.Attributes.Any(attribute => attribute.Property.Name == nameof(Identifiable.Id)))
            {
                throw new JsonApiSerializationException("Resource ID is read-only.", null, atomicOperationIndex: AtomicOperationIndex);
            }
        }

        /// <summary>
        /// Additional processing required for server deserialization. Flags a processed attribute or relationship as updated using
        /// <see cref="ITargetedFields" />.
        /// </summary>
        /// <param name="resource">
        /// The resource that was constructed from the document's body.
        /// </param>
        /// <param name="field">
        /// The metadata for the exposed field.
        /// </param>
        /// <param name="data">
        /// Relationship data for <paramref name="resource" />. Is null when <paramref name="field" /> is not a <see cref="RelationshipAttribute" />.
        /// </param>
        protected override void AfterProcessField(IIdentifiable resource, ResourceFieldAttribute field, RelationshipEntry data = null)
        {
            bool isCreatingResource = IsCreatingResource();
            bool isUpdatingResource = IsUpdatingResource();

            if (field is AttrAttribute attr)
            {
                if (isCreatingResource && !attr.Capabilities.HasFlag(AttrCapabilities.AllowCreate))
                {
                    throw new JsonApiSerializationException("Setting the initial value of the requested attribute is not allowed.",
                        $"Setting the initial value of '{attr.PublicName}' is not allowed.", atomicOperationIndex: AtomicOperationIndex);
                }

                if (isUpdatingResource && !attr.Capabilities.HasFlag(AttrCapabilities.AllowChange))
                {
                    throw new JsonApiSerializationException("Changing the value of the requested attribute is not allowed.",
                        $"Changing the value of '{attr.PublicName}' is not allowed.", atomicOperationIndex: AtomicOperationIndex);
                }

                _targetedFields.Attributes.Add(attr);
            }
            else if (field is RelationshipAttribute relationship)
            {
                _targetedFields.Relationships.Add(relationship);
            }
        }

        private bool IsCreatingResource()
        {
            return _request.Kind == EndpointKind.AtomicOperations
                ? _request.OperationKind == OperationKind.CreateResource
                : _request.Kind == EndpointKind.Primary && _httpContextAccessor.HttpContext!.Request.Method == HttpMethod.Post.Method;
        }

        private bool IsUpdatingResource()
        {
            return _request.Kind == EndpointKind.AtomicOperations
                ? _request.OperationKind == OperationKind.UpdateResource
                : _request.Kind == EndpointKind.Primary && _httpContextAccessor.HttpContext!.Request.Method == HttpMethod.Patch.Method;
        }
    }
}
