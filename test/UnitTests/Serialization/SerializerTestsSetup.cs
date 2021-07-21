using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using Moq;

namespace UnitTests.Serialization
{
    public class SerializerTestsSetup : SerializationTestsSetupBase
    {
        private static readonly IncludeChainConverter IncludeChainConverter = new();

        protected readonly TopLevelLinks DummyTopLevelLinks;
        protected readonly ResourceLinks DummyResourceLinks;
        protected readonly RelationshipLinks DummyRelationshipLinks;

        protected SerializerTestsSetup()
        {
            DummyTopLevelLinks = new TopLevelLinks
            {
                Self = "http://www.dummy.com/dummy-self-link",
                Next = "http://www.dummy.com/dummy-next-link",
                Prev = "http://www.dummy.com/dummy-prev-link",
                First = "http://www.dummy.com/dummy-first-link",
                Last = "http://www.dummy.com/dummy-last-link"
            };

            DummyResourceLinks = new ResourceLinks
            {
                Self = "http://www.dummy.com/dummy-resource-self-link"
            };

            DummyRelationshipLinks = new RelationshipLinks
            {
                Related = "http://www.dummy.com/dummy-relationship-related-link",
                Self = "http://www.dummy.com/dummy-relationship-self-link"
            };
        }

        protected ResponseSerializer<T> GetResponseSerializer<T>(IEnumerable<IEnumerable<RelationshipAttribute>> inclusionChains = null,
            Dictionary<string, object> metaDict = null, TopLevelLinks topLinks = null, ResourceLinks resourceLinks = null,
            RelationshipLinks relationshipLinks = null)
            where T : class, IIdentifiable
        {
            IEnumerable<RelationshipAttribute>[] inclusionChainArray = inclusionChains?.ToArray();

            IMetaBuilder meta = GetMetaBuilder(metaDict);
            ILinkBuilder link = GetLinkBuilder(topLinks, resourceLinks, relationshipLinks);
            IEnumerable<IQueryConstraintProvider> includeConstraints = GetIncludeConstraints(inclusionChainArray);
            IIncludedResourceObjectBuilder includedBuilder = GetIncludedBuilder(inclusionChainArray != null);
            IFieldsToSerialize fieldsToSerialize = GetSerializableFields();
            IResourceDefinitionAccessor resourceDefinitionAccessor = GetResourceDefinitionAccessor();
            IResourceObjectBuilderSettingsProvider settingsProvider = GetSerializerSettingsProvider();
            IEvaluatedIncludeCache evaluatedIncludeCache = GetEvaluatedIncludeCache(inclusionChainArray);

            var resourceObjectBuilder = new ResponseResourceObjectBuilder(link, includedBuilder, includeConstraints, ResourceGraph, resourceDefinitionAccessor,
                settingsProvider, evaluatedIncludeCache);

            var jsonApiOptions = new JsonApiOptions();

            return new ResponseSerializer<T>(meta, link, includedBuilder, fieldsToSerialize, resourceObjectBuilder, resourceDefinitionAccessor, jsonApiOptions);
        }

        protected ResponseResourceObjectBuilder GetResponseResourceObjectBuilder(IEnumerable<IEnumerable<RelationshipAttribute>> inclusionChains = null,
            ResourceLinks resourceLinks = null, RelationshipLinks relationshipLinks = null)
        {
            IEnumerable<RelationshipAttribute>[] inclusionChainArray = inclusionChains?.ToArray();

            ILinkBuilder link = GetLinkBuilder(null, resourceLinks, relationshipLinks);
            IEnumerable<IQueryConstraintProvider> includeConstraints = GetIncludeConstraints(inclusionChainArray);
            IIncludedResourceObjectBuilder includedBuilder = GetIncludedBuilder(inclusionChains != null);
            IEvaluatedIncludeCache evaluatedIncludeCache = GetEvaluatedIncludeCache(inclusionChainArray);

            return new ResponseResourceObjectBuilder(link, includedBuilder, includeConstraints, ResourceGraph, GetResourceDefinitionAccessor(),
                GetSerializerSettingsProvider(), evaluatedIncludeCache);
        }

        private IIncludedResourceObjectBuilder GetIncludedBuilder(bool hasIncludeQueryString)
        {
            IFieldsToSerialize fieldsToSerialize = GetSerializableFields();
            ILinkBuilder linkBuilder = GetLinkBuilder();
            IResourceDefinitionAccessor resourceDefinitionAccessor = GetResourceDefinitionAccessor();
            IRequestQueryStringAccessor queryStringAccessor = new FakeRequestQueryStringAccessor(hasIncludeQueryString ? "include=" : null);
            IResourceObjectBuilderSettingsProvider resourceObjectBuilderSettingsProvider = GetSerializerSettingsProvider();

            return new IncludedResourceObjectBuilder(fieldsToSerialize, linkBuilder, ResourceGraph, Enumerable.Empty<IQueryConstraintProvider>(),
                resourceDefinitionAccessor, queryStringAccessor, resourceObjectBuilderSettingsProvider);
        }

        protected IResourceObjectBuilderSettingsProvider GetSerializerSettingsProvider()
        {
            var mock = new Mock<IResourceObjectBuilderSettingsProvider>();
            mock.Setup(provider => provider.Get()).Returns(new ResourceObjectBuilderSettings());
            return mock.Object;
        }

        private IResourceDefinitionAccessor GetResourceDefinitionAccessor()
        {
            var mock = new Mock<IResourceDefinitionAccessor>();
            return mock.Object;
        }

        private IMetaBuilder GetMetaBuilder(Dictionary<string, object> meta = null)
        {
            var mock = new Mock<IMetaBuilder>();
            mock.Setup(metaBuilder => metaBuilder.Build()).Returns(meta);
            return mock.Object;
        }

        protected ILinkBuilder GetLinkBuilder(TopLevelLinks top = null, ResourceLinks resource = null, RelationshipLinks relationship = null)
        {
            var mock = new Mock<ILinkBuilder>();
            mock.Setup(linkBuilder => linkBuilder.GetTopLevelLinks()).Returns(top);
            mock.Setup(linkBuilder => linkBuilder.GetResourceLinks(It.IsAny<string>(), It.IsAny<string>())).Returns(resource);
            mock.Setup(linkBuilder => linkBuilder.GetRelationshipLinks(It.IsAny<RelationshipAttribute>(), It.IsAny<IIdentifiable>())).Returns(relationship);
            return mock.Object;
        }

        protected IFieldsToSerialize GetSerializableFields()
        {
            var mock = new Mock<IFieldsToSerialize>();
            mock.Setup(fields => fields.GetAttributes(It.IsAny<Type>())).Returns<Type>(type => ResourceGraph.GetResourceContext(type).Attributes);
            mock.Setup(fields => fields.GetRelationships(It.IsAny<Type>())).Returns<Type>(type => ResourceGraph.GetResourceContext(type).Relationships);
            return mock.Object;
        }

        private IEnumerable<IQueryConstraintProvider> GetIncludeConstraints(IEnumerable<IEnumerable<RelationshipAttribute>> inclusionChains = null)
        {
            var expressionsInScope = new List<ExpressionInScope>();

            if (inclusionChains != null)
            {
                List<ResourceFieldChainExpression> chains = inclusionChains.Select(relationships => new ResourceFieldChainExpression(relationships.ToArray()))
                    .ToList();

                IncludeExpression includeExpression = IncludeChainConverter.FromRelationshipChains(chains);
                expressionsInScope.Add(new ExpressionInScope(null, includeExpression));
            }

            var mock = new Mock<IQueryConstraintProvider>();
            mock.Setup(provider => provider.GetConstraints()).Returns(expressionsInScope);

            IQueryConstraintProvider includeConstraintProvider = mock.Object;
            return includeConstraintProvider.AsEnumerable();
        }

        private IEvaluatedIncludeCache GetEvaluatedIncludeCache(IEnumerable<IEnumerable<RelationshipAttribute>> inclusionChains = null)
        {
            if (inclusionChains == null)
            {
                return new EvaluatedIncludeCache();
            }

            List<ResourceFieldChainExpression> chains = inclusionChains.Select(relationships => new ResourceFieldChainExpression(relationships.ToArray()))
                .ToList();

            IncludeExpression includeExpression = IncludeChainConverter.FromRelationshipChains(chains);

            var evaluatedIncludeCache = new EvaluatedIncludeCache();
            evaluatedIncludeCache.Set(includeExpression);
            return evaluatedIncludeCache;
        }

        /// <summary>
        /// Minimal implementation of abstract JsonApiSerializer base class, with the purpose of testing the business logic for building the document structure.
        /// </summary>
        protected sealed class TestSerializer : BaseSerializer
        {
            public TestSerializer(IResourceObjectBuilder resourceObjectBuilder)
                : base(resourceObjectBuilder)
            {
            }

            public Document PublicBuild(IIdentifiable resource, IReadOnlyCollection<AttrAttribute> attributes = null,
                IReadOnlyCollection<RelationshipAttribute> relationships = null)
            {
                return Build(resource, attributes, relationships);
            }

            public Document PublicBuild(IReadOnlyCollection<IIdentifiable> resources, IReadOnlyCollection<AttrAttribute> attributes = null,
                IReadOnlyCollection<RelationshipAttribute> relationships = null)
            {
                return Build(resources, attributes, relationships);
            }
        }
    }
}
