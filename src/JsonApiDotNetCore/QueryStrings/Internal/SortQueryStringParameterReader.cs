using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal.Parsing;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    [PublicAPI]
    public class SortQueryStringParameterReader : QueryStringParameterReader, ISortQueryStringParameterReader
    {
        private readonly QueryStringParameterScopeParser _scopeParser;
        private readonly SortParser _sortParser;
        private readonly List<ExpressionInScope> _constraints = new();
        private string _lastParameterName;

        public SortQueryStringParameterReader(IJsonApiRequest request, IResourceContextProvider resourceContextProvider)
            : base(request, resourceContextProvider)
        {
            _scopeParser = new QueryStringParameterScopeParser(resourceContextProvider, FieldChainRequirements.EndsInToMany);
            _sortParser = new SortParser(resourceContextProvider, ValidateSingleField);
        }

        protected void ValidateSingleField(ResourceFieldAttribute field, ResourceContext resourceContext, string path)
        {
            if (field is AttrAttribute attribute && !attribute.Capabilities.HasFlag(AttrCapabilities.AllowSort))
            {
                throw new InvalidQueryStringParameterException(_lastParameterName, "Sorting on the requested attribute is not allowed.",
                    $"Sorting on attribute '{attribute.PublicName}' is not allowed.");
            }
        }

        /// <inheritdoc />
        public virtual bool IsEnabled(DisableQueryStringAttribute disableQueryStringAttribute)
        {
            ArgumentGuard.NotNull(disableQueryStringAttribute, nameof(disableQueryStringAttribute));

            return !IsAtomicOperationsRequest && !disableQueryStringAttribute.ContainsParameter(StandardQueryStringParameters.Sort);
        }

        /// <inheritdoc />
        public virtual bool CanRead(string parameterName)
        {
            ArgumentGuard.NotNullNorEmpty(parameterName, nameof(parameterName));

            bool isNested = parameterName.StartsWith("sort[", StringComparison.Ordinal) && parameterName.EndsWith("]", StringComparison.Ordinal);
            return parameterName == "sort" || isNested;
        }

        /// <inheritdoc />
        public virtual void Read(string parameterName, StringValues parameterValue)
        {
            _lastParameterName = parameterName;

            try
            {
                ResourceFieldChainExpression scope = GetScope(parameterName);
                SortExpression sort = GetSort(parameterValue, scope);

                var expressionInScope = new ExpressionInScope(scope, sort);
                _constraints.Add(expressionInScope);
            }
            catch (QueryParseException exception)
            {
                throw new InvalidQueryStringParameterException(parameterName, "The specified sort is invalid.", exception.Message, exception);
            }
        }

        private ResourceFieldChainExpression GetScope(string parameterName)
        {
            QueryStringParameterScopeExpression parameterScope = _scopeParser.Parse(parameterName, RequestResource);

            if (parameterScope.Scope == null)
            {
                AssertIsCollectionRequest();
            }

            return parameterScope.Scope;
        }

        private SortExpression GetSort(string parameterValue, ResourceFieldChainExpression scope)
        {
            ResourceContext resourceContextInScope = GetResourceContextForScope(scope);
            return _sortParser.Parse(parameterValue, resourceContextInScope);
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<ExpressionInScope> GetConstraints()
        {
            return _constraints;
        }
    }
}
