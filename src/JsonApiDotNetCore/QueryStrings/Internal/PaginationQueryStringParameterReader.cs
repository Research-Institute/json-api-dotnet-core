using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal.Parsing;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    [PublicAPI]
    public class PaginationQueryStringParameterReader : QueryStringParameterReader, IPaginationQueryStringParameterReader
    {
        private const string PageSizeParameterName = "page[size]";
        private const string PageNumberParameterName = "page[number]";

        private readonly IJsonApiOptions _options;
        private readonly PaginationParser _paginationParser;

        private PaginationQueryStringValueExpression _pageSizeConstraint;
        private PaginationQueryStringValueExpression _pageNumberConstraint;

        public PaginationQueryStringParameterReader(IJsonApiRequest request, IResourceContextProvider resourceContextProvider, IJsonApiOptions options)
            : base(request, resourceContextProvider)
        {
            ArgumentGuard.NotNull(options, nameof(options));

            _options = options;
            _paginationParser = new PaginationParser(resourceContextProvider);
        }

        /// <inheritdoc />
        public virtual bool IsEnabled(DisableQueryStringAttribute disableQueryStringAttribute)
        {
            ArgumentGuard.NotNull(disableQueryStringAttribute, nameof(disableQueryStringAttribute));

            return !IsAtomicOperationsRequest && !disableQueryStringAttribute.ContainsParameter(StandardQueryStringParameters.Page);
        }

        /// <inheritdoc />
        public virtual bool CanRead(string parameterName)
        {
            return parameterName == PageSizeParameterName || parameterName == PageNumberParameterName;
        }

        /// <inheritdoc />
        public virtual void Read(string parameterName, StringValues parameterValue)
        {
            try
            {
                PaginationQueryStringValueExpression constraint = GetPageConstraint(parameterValue);

                if (constraint.Elements.Any(element => element.Scope == null))
                {
                    AssertIsCollectionRequest();
                }

                if (parameterName == PageSizeParameterName)
                {
                    ValidatePageSize(constraint);
                    _pageSizeConstraint = constraint;
                }
                else
                {
                    ValidatePageNumber(constraint);
                    _pageNumberConstraint = constraint;
                }
            }
            catch (QueryParseException exception)
            {
                throw new InvalidQueryStringParameterException(parameterName, "The specified paging is invalid.", exception.Message, exception);
            }
        }

        private PaginationQueryStringValueExpression GetPageConstraint(string parameterValue)
        {
            return _paginationParser.Parse(parameterValue, RequestResource);
        }

        protected virtual void ValidatePageSize(PaginationQueryStringValueExpression constraint)
        {
            if (_options.MaximumPageSize != null)
            {
                if (constraint.Elements.Any(element => element.Value > _options.MaximumPageSize.Value))
                {
                    throw new QueryParseException($"Page size cannot be higher than {_options.MaximumPageSize}.");
                }

                if (constraint.Elements.Any(element => element.Value == 0))
                {
                    throw new QueryParseException("Page size cannot be unconstrained.");
                }
            }

            if (constraint.Elements.Any(element => element.Value < 0))
            {
                throw new QueryParseException("Page size cannot be negative.");
            }
        }

        [AssertionMethod]
        protected virtual void ValidatePageNumber(PaginationQueryStringValueExpression constraint)
        {
            if (_options.MaximumPageNumber != null && constraint.Elements.Any(element => element.Value > _options.MaximumPageNumber.OneBasedValue))
            {
                throw new QueryParseException($"Page number cannot be higher than {_options.MaximumPageNumber}.");
            }

            if (constraint.Elements.Any(element => element.Value < 1))
            {
                throw new QueryParseException("Page number cannot be negative or zero.");
            }
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<ExpressionInScope> GetConstraints()
        {
            var context = new PaginationContext();

            foreach (PaginationElementQueryStringValueExpression element in _pageSizeConstraint?.Elements ??
                Array.Empty<PaginationElementQueryStringValueExpression>())
            {
                MutablePaginationEntry entry = context.ResolveEntryInScope(element.Scope);
                entry.PageSize = element.Value == 0 ? null : new PageSize(element.Value);
                entry.HasSetPageSize = true;
            }

            foreach (PaginationElementQueryStringValueExpression element in _pageNumberConstraint?.Elements ??
                Array.Empty<PaginationElementQueryStringValueExpression>())
            {
                MutablePaginationEntry entry = context.ResolveEntryInScope(element.Scope);
                entry.PageNumber = new PageNumber(element.Value);
            }

            context.ApplyOptions(_options);

            return context.GetExpressionsInScope();
        }

        private sealed class PaginationContext
        {
            private readonly MutablePaginationEntry _globalScope = new();
            private readonly Dictionary<ResourceFieldChainExpression, MutablePaginationEntry> _nestedScopes = new();

            public MutablePaginationEntry ResolveEntryInScope(ResourceFieldChainExpression scope)
            {
                if (scope == null)
                {
                    return _globalScope;
                }

                if (!_nestedScopes.ContainsKey(scope))
                {
                    _nestedScopes.Add(scope, new MutablePaginationEntry());
                }

                return _nestedScopes[scope];
            }

            public void ApplyOptions(IJsonApiOptions options)
            {
                ApplyOptionsInEntry(_globalScope, options);

                foreach ((_, MutablePaginationEntry entry) in _nestedScopes)
                {
                    ApplyOptionsInEntry(entry, options);
                }
            }

            private void ApplyOptionsInEntry(MutablePaginationEntry entry, IJsonApiOptions options)
            {
                if (!entry.HasSetPageSize)
                {
                    entry.PageSize = options.DefaultPageSize;
                }

                entry.PageNumber ??= PageNumber.ValueOne;
            }

            public IReadOnlyCollection<ExpressionInScope> GetExpressionsInScope()
            {
                return EnumerateExpressionsInScope().ToArray();
            }

            private IEnumerable<ExpressionInScope> EnumerateExpressionsInScope()
            {
                yield return new ExpressionInScope(null, new PaginationExpression(_globalScope.PageNumber, _globalScope.PageSize));

                foreach ((ResourceFieldChainExpression scope, MutablePaginationEntry entry) in _nestedScopes)
                {
                    yield return new ExpressionInScope(scope, new PaginationExpression(entry.PageNumber, entry.PageSize));
                }
            }
        }

        private sealed class MutablePaginationEntry
        {
            public PageSize PageSize { get; set; }
            public bool HasSetPageSize { get; set; }

            public PageNumber PageNumber { get; set; }
        }
    }
}
