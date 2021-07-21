using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using BenchmarkDotNet.Attributes;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.QueryStrings.Internal;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks.Query
{
    // ReSharper disable once ClassCanBeSealed.Global
    [MarkdownExporter]
    [SimpleJob(3, 10, 20)]
    [MemoryDiagnoser]
    public class QueryParserBenchmarks
    {
        private readonly DependencyFactory _dependencyFactory = new();
        private readonly FakeRequestQueryStringAccessor _queryStringAccessor = new();
        private readonly QueryStringReader _queryStringReaderForSort;
        private readonly QueryStringReader _queryStringReaderForAll;

        public QueryParserBenchmarks()
        {
            IJsonApiOptions options = new JsonApiOptions
            {
                EnableLegacyFilterNotation = true
            };

            IResourceGraph resourceGraph = _dependencyFactory.CreateResourceGraph(options);

            var request = new JsonApiRequest
            {
                PrimaryResource = resourceGraph.GetResourceContext(typeof(BenchmarkResource)),
                IsCollection = true
            };

            _queryStringReaderForSort = CreateQueryParameterDiscoveryForSort(resourceGraph, request, options, _queryStringAccessor);
            _queryStringReaderForAll = CreateQueryParameterDiscoveryForAll(resourceGraph, request, options, _queryStringAccessor);
        }

        private static QueryStringReader CreateQueryParameterDiscoveryForSort(IResourceGraph resourceGraph, JsonApiRequest request, IJsonApiOptions options,
            FakeRequestQueryStringAccessor queryStringAccessor)
        {
            var sortReader = new SortQueryStringParameterReader(request, resourceGraph);

            IEnumerable<SortQueryStringParameterReader> readers = sortReader.AsEnumerable();

            return new QueryStringReader(options, queryStringAccessor, readers, NullLoggerFactory.Instance);
        }

        private static QueryStringReader CreateQueryParameterDiscoveryForAll(IResourceGraph resourceGraph, JsonApiRequest request, IJsonApiOptions options,
            FakeRequestQueryStringAccessor queryStringAccessor)
        {
            var resourceFactory = new ResourceFactory(new ServiceContainer());

            var includeReader = new IncludeQueryStringParameterReader(request, resourceGraph, options);
            var filterReader = new FilterQueryStringParameterReader(request, resourceGraph, resourceFactory, options);
            var sortReader = new SortQueryStringParameterReader(request, resourceGraph);
            var sparseFieldSetReader = new SparseFieldSetQueryStringParameterReader(request, resourceGraph);
            var paginationReader = new PaginationQueryStringParameterReader(request, resourceGraph, options);
            var defaultsReader = new DefaultsQueryStringParameterReader(options);
            var nullsReader = new NullsQueryStringParameterReader(options);

            IQueryStringParameterReader[] readers = ArrayFactory.Create<IQueryStringParameterReader>(includeReader, filterReader, sortReader,
                sparseFieldSetReader, paginationReader, defaultsReader, nullsReader);

            return new QueryStringReader(options, queryStringAccessor, readers, NullLoggerFactory.Instance);
        }

        [Benchmark]
        public void AscendingSort()
        {
            string queryString = $"?sort={BenchmarkResourcePublicNames.NameAttr}";

            _queryStringAccessor.SetQueryString(queryString);
            _queryStringReaderForSort.ReadAll(null);
        }

        [Benchmark]
        public void DescendingSort()
        {
            string queryString = $"?sort=-{BenchmarkResourcePublicNames.NameAttr}";

            _queryStringAccessor.SetQueryString(queryString);
            _queryStringReaderForSort.ReadAll(null);
        }

        [Benchmark]
        public void ComplexQuery()
        {
            Run(100, () =>
            {
                const string resourceName = BenchmarkResourcePublicNames.Type;
                const string attrName = BenchmarkResourcePublicNames.NameAttr;

                string queryString = $"?filter[{attrName}]=abc,eq:abc&sort=-{attrName}&include=child&page[size]=1&fields[{resourceName}]={attrName}";

                _queryStringAccessor.SetQueryString(queryString);
                _queryStringReaderForAll.ReadAll(null);
            });
        }

        private void Run(int iterations, Action action)
        {
            for (int index = 0; index < iterations; index++)
            {
                action();
            }
        }

        private sealed class FakeRequestQueryStringAccessor : IRequestQueryStringAccessor
        {
            public IQueryCollection Query { get; private set; }

            public void SetQueryString(string queryString)
            {
                Query = new QueryCollection(QueryHelpers.ParseQuery(queryString));
            }
        }
    }
}
