using System.Collections.Generic;
using JetBrains.Annotations;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Resources
{
    /// <summary>
    /// Represents a write operation on a JSON:API resource.
    /// </summary>
    [PublicAPI]
    public sealed class OperationContainer
    {
        private static readonly CollectionConverter CollectionConverter = new();

        public OperationKind Kind { get; }
        public IIdentifiable Resource { get; }
        public ITargetedFields TargetedFields { get; }
        public IJsonApiRequest Request { get; }

        public OperationContainer(OperationKind kind, IIdentifiable resource, ITargetedFields targetedFields, IJsonApiRequest request)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));
            ArgumentGuard.NotNull(targetedFields, nameof(targetedFields));
            ArgumentGuard.NotNull(request, nameof(request));

            Kind = kind;
            Resource = resource;
            TargetedFields = targetedFields;
            Request = request;
        }

        public void SetTransactionId(string transactionId)
        {
            ((JsonApiRequest)Request).TransactionId = transactionId;
        }

        public OperationContainer WithResource(IIdentifiable resource)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));

            return new OperationContainer(Kind, resource, TargetedFields, Request);
        }

        public ISet<IIdentifiable> GetSecondaryResources()
        {
            var secondaryResources = new HashSet<IIdentifiable>(IdentifiableComparer.Instance);

            foreach (RelationshipAttribute relationship in TargetedFields.Relationships)
            {
                AddSecondaryResources(relationship, secondaryResources);
            }

            return secondaryResources;
        }

        private void AddSecondaryResources(RelationshipAttribute relationship, HashSet<IIdentifiable> secondaryResources)
        {
            object rightValue = relationship.GetValue(Resource);

            foreach (IIdentifiable rightResource in CollectionConverter.ExtractResources(rightValue))
            {
                secondaryResources.Add(rightResource);
            }
        }
    }
}
