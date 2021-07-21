using System;
using BenchmarkDotNet.Attributes;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.QueryStrings.Internal;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using Moq;

namespace Benchmarks.Serialization
{
    // ReSharper disable once ClassCanBeSealed.Global
    [MarkdownExporter]
    public class JsonApiSerializerBenchmarks
    {
        private static readonly BenchmarkResource Content = new()
        {
            Id = 123,
            Name = Guid.NewGuid().ToString()
        };

        private readonly DependencyFactory _dependencyFactory = new();
        private readonly IJsonApiSerializer _jsonApiSerializer;

        public JsonApiSerializerBenchmarks()
        {
            var options = new JsonApiOptions();
            IResourceGraph resourceGraph = _dependencyFactory.CreateResourceGraph(options);
            IFieldsToSerialize fieldsToSerialize = CreateFieldsToSerialize(resourceGraph);

            IMetaBuilder metaBuilder = new Mock<IMetaBuilder>().Object;
            ILinkBuilder linkBuilder = new Mock<ILinkBuilder>().Object;
            IIncludedResourceObjectBuilder includeBuilder = new Mock<IIncludedResourceObjectBuilder>().Object;

            var resourceObjectBuilder = new ResourceObjectBuilder(resourceGraph, new ResourceObjectBuilderSettings());

            IResourceDefinitionAccessor resourceDefinitionAccessor = new Mock<IResourceDefinitionAccessor>().Object;

            _jsonApiSerializer = new ResponseSerializer<BenchmarkResource>(metaBuilder, linkBuilder, includeBuilder, fieldsToSerialize, resourceObjectBuilder,
                resourceDefinitionAccessor, options);
        }

        private static FieldsToSerialize CreateFieldsToSerialize(IResourceGraph resourceGraph)
        {
            var request = new JsonApiRequest();

            var constraintProviders = new IQueryConstraintProvider[]
            {
                new SparseFieldSetQueryStringParameterReader(request, resourceGraph)
            };

            IResourceDefinitionAccessor accessor = new Mock<IResourceDefinitionAccessor>().Object;

            return new FieldsToSerialize(resourceGraph, constraintProviders, accessor, request);
        }

        [Benchmark]
        public object SerializeSimpleObject()
        {
            return _jsonApiSerializer.Serialize(Content);
        }
    }
}
