using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Serialization.Building
{
    [PublicAPI]
    public class ResponseResourceObjectBuilder : ResourceObjectBuilder
    {
        private static readonly IncludeChainConverter IncludeChainConverter = new();

        private readonly ILinkBuilder _linkBuilder;
        private readonly IIncludedResourceObjectBuilder _includedBuilder;
        private readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        private readonly IEvaluatedIncludeCache _evaluatedIncludeCache;
        private readonly SparseFieldSetCache _sparseFieldSetCache;

        private RelationshipAttribute _requestRelationship;

        public ResponseResourceObjectBuilder(ILinkBuilder linkBuilder, IIncludedResourceObjectBuilder includedBuilder,
            IEnumerable<IQueryConstraintProvider> constraintProviders, IResourceContextProvider resourceContextProvider,
            IResourceDefinitionAccessor resourceDefinitionAccessor, IResourceObjectBuilderSettingsProvider settingsProvider,
            IEvaluatedIncludeCache evaluatedIncludeCache)
            : base(resourceContextProvider, settingsProvider.Get())
        {
            ArgumentGuard.NotNull(linkBuilder, nameof(linkBuilder));
            ArgumentGuard.NotNull(includedBuilder, nameof(includedBuilder));
            ArgumentGuard.NotNull(constraintProviders, nameof(constraintProviders));
            ArgumentGuard.NotNull(resourceDefinitionAccessor, nameof(resourceDefinitionAccessor));
            ArgumentGuard.NotNull(evaluatedIncludeCache, nameof(evaluatedIncludeCache));

            _linkBuilder = linkBuilder;
            _includedBuilder = includedBuilder;
            _resourceDefinitionAccessor = resourceDefinitionAccessor;
            _evaluatedIncludeCache = evaluatedIncludeCache;
            _sparseFieldSetCache = new SparseFieldSetCache(constraintProviders, resourceDefinitionAccessor);
        }

        public RelationshipEntry Build(IIdentifiable resource, RelationshipAttribute requestRelationship)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));
            ArgumentGuard.NotNull(requestRelationship, nameof(requestRelationship));

            _requestRelationship = requestRelationship;
            return GetRelationshipData(requestRelationship, resource);
        }

        /// <inheritdoc />
        public override ResourceObject Build(IIdentifiable resource, IReadOnlyCollection<AttrAttribute> attributes = null,
            IReadOnlyCollection<RelationshipAttribute> relationships = null)
        {
            ResourceObject resourceObject = base.Build(resource, attributes, relationships);

            resourceObject.Meta = _resourceDefinitionAccessor.GetMeta(resource.GetType(), resource);

            return resourceObject;
        }

        /// <summary>
        /// Builds the values of the relationships object on a resource object. The server serializer only populates the "data" member when the relationship is
        /// included, and adds links unless these are turned off. This means that if a relationship is not included and links are turned off, the entry would be
        /// completely empty, ie { }, which is not conform JSON:API spec. In that case we return null which will omit the entry from the output.
        /// </summary>
        protected override RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable resource)
        {
            ArgumentGuard.NotNull(relationship, nameof(relationship));
            ArgumentGuard.NotNull(resource, nameof(resource));

            RelationshipEntry relationshipEntry = null;
            IReadOnlyCollection<IReadOnlyCollection<RelationshipAttribute>> relationshipChains = GetInclusionChainsStartingWith(relationship);

            if (Equals(relationship, _requestRelationship) || relationshipChains.Any())
            {
                relationshipEntry = base.GetRelationshipData(relationship, resource);

                if (relationshipChains.Any() && relationshipEntry.HasResource)
                {
                    foreach (IReadOnlyCollection<RelationshipAttribute> chain in relationshipChains)
                    {
                        // traverses (recursively) and extracts all (nested) related resources for the current inclusion chain.
                        _includedBuilder.IncludeRelationshipChain(chain, resource);
                    }
                }
            }

            if (!IsRelationshipInSparseFieldSet(relationship))
            {
                return null;
            }

            RelationshipLinks links = _linkBuilder.GetRelationshipLinks(relationship, resource);

            if (links != null)
            {
                // if relationshipLinks should be built for this entry, populate the "links" field.
                relationshipEntry ??= new RelationshipEntry();
                relationshipEntry.Links = links;
            }

            // if neither "links" nor "data" was populated, return null, which will omit this entry from the output.
            // (see the NullValueHandling settings on <see cref="ResourceObject"/>)
            return relationshipEntry;
        }

        private bool IsRelationshipInSparseFieldSet(RelationshipAttribute relationship)
        {
            ResourceContext resourceContext = ResourceContextProvider.GetResourceContext(relationship.LeftType);

            IReadOnlyCollection<ResourceFieldAttribute> fieldSet = _sparseFieldSetCache.GetSparseFieldSetForSerializer(resourceContext);
            return fieldSet.Contains(relationship);
        }

        /// <summary>
        /// Inspects the included relationship chains and selects the ones that starts with the specified relationship.
        /// </summary>
        private IReadOnlyCollection<IReadOnlyCollection<RelationshipAttribute>> GetInclusionChainsStartingWith(RelationshipAttribute relationship)
        {
            IncludeExpression include = _evaluatedIncludeCache.Get() ?? IncludeExpression.Empty;
            IReadOnlyCollection<ResourceFieldChainExpression> chains = IncludeChainConverter.GetRelationshipChains(include);

            var inclusionChains = new List<IReadOnlyCollection<RelationshipAttribute>>();

            foreach (ResourceFieldChainExpression chain in chains)
            {
                if (chain.Fields.First().Equals(relationship))
                {
                    inclusionChains.Add(chain.Fields.Cast<RelationshipAttribute>().ToArray());
                }
            }

            return inclusionChains;
        }
    }
}
