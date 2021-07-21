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
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    [PublicAPI]
    public class SparseFieldSetQueryStringParameterReader : QueryStringParameterReader, ISparseFieldSetQueryStringParameterReader
    {
        private readonly SparseFieldTypeParser _sparseFieldTypeParser;
        private readonly SparseFieldSetParser _sparseFieldSetParser;
        private readonly Dictionary<ResourceContext, SparseFieldSetExpression> _sparseFieldTable = new();
        private string _lastParameterName;

        /// <inheritdoc />
        bool IQueryStringParameterReader.AllowEmptyValue => true;

        public SparseFieldSetQueryStringParameterReader(IJsonApiRequest request, IResourceContextProvider resourceContextProvider)
            : base(request, resourceContextProvider)
        {
            _sparseFieldTypeParser = new SparseFieldTypeParser(resourceContextProvider);
            _sparseFieldSetParser = new SparseFieldSetParser(resourceContextProvider, ValidateSingleField);
        }

        protected void ValidateSingleField(ResourceFieldAttribute field, ResourceContext resourceContext, string path)
        {
            if (field is AttrAttribute attribute && !attribute.Capabilities.HasFlag(AttrCapabilities.AllowView))
            {
                throw new InvalidQueryStringParameterException(_lastParameterName, "Retrieving the requested attribute is not allowed.",
                    $"Retrieving the attribute '{attribute.PublicName}' is not allowed.");
            }
        }

        /// <inheritdoc />
        public virtual bool IsEnabled(DisableQueryStringAttribute disableQueryStringAttribute)
        {
            ArgumentGuard.NotNull(disableQueryStringAttribute, nameof(disableQueryStringAttribute));

            return !IsAtomicOperationsRequest && !disableQueryStringAttribute.ContainsParameter(StandardQueryStringParameters.Fields);
        }

        /// <inheritdoc />
        public virtual bool CanRead(string parameterName)
        {
            ArgumentGuard.NotNullNorEmpty(parameterName, nameof(parameterName));

            return parameterName.StartsWith("fields[", StringComparison.Ordinal) && parameterName.EndsWith("]", StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public virtual void Read(string parameterName, StringValues parameterValue)
        {
            _lastParameterName = parameterName;

            try
            {
                ResourceContext targetResource = GetSparseFieldType(parameterName);
                SparseFieldSetExpression sparseFieldSet = GetSparseFieldSet(parameterValue, targetResource);

                _sparseFieldTable[targetResource] = sparseFieldSet;
            }
            catch (QueryParseException exception)
            {
                throw new InvalidQueryStringParameterException(parameterName, "The specified fieldset is invalid.", exception.Message, exception);
            }
        }

        private ResourceContext GetSparseFieldType(string parameterName)
        {
            return _sparseFieldTypeParser.Parse(parameterName);
        }

        private SparseFieldSetExpression GetSparseFieldSet(string parameterValue, ResourceContext resourceContext)
        {
            SparseFieldSetExpression sparseFieldSet = _sparseFieldSetParser.Parse(parameterValue, resourceContext);

            if (sparseFieldSet == null)
            {
                // We add ID on an incoming empty fieldset, so that callers can distinguish between no fieldset and an empty one.
                AttrAttribute idAttribute = resourceContext.Attributes.Single(attribute => attribute.Property.Name == nameof(Identifiable.Id));
                return new SparseFieldSetExpression(ArrayFactory.Create(idAttribute));
            }

            return sparseFieldSet;
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<ExpressionInScope> GetConstraints()
        {
            return _sparseFieldTable.Any()
                ? new ExpressionInScope(null, new SparseFieldTableExpression(_sparseFieldTable)).AsArray()
                : Array.Empty<ExpressionInScope>();
        }
    }
}
