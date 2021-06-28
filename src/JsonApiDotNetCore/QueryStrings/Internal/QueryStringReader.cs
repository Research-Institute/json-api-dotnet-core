using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Diagnostics;
using JsonApiDotNetCore.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    /// <inheritdoc />
    [PublicAPI]
    public class QueryStringReader : IQueryStringReader
    {
        private readonly IJsonApiOptions _options;
        private readonly IRequestQueryStringAccessor _queryStringAccessor;
        private readonly IEnumerable<IQueryStringParameterReader> _parameterReaders;
        private readonly ILogger<QueryStringReader> _logger;

        public QueryStringReader(IJsonApiOptions options, IRequestQueryStringAccessor queryStringAccessor,
            IEnumerable<IQueryStringParameterReader> parameterReaders, ILoggerFactory loggerFactory)
        {
            ArgumentGuard.NotNull(loggerFactory, nameof(loggerFactory));
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(queryStringAccessor, nameof(queryStringAccessor));
            ArgumentGuard.NotNull(parameterReaders, nameof(parameterReaders));

            _options = options;
            _queryStringAccessor = queryStringAccessor;
            _parameterReaders = parameterReaders;
            _logger = loggerFactory.CreateLogger<QueryStringReader>();
        }

        /// <inheritdoc />
        public virtual void ReadAll(DisableQueryStringAttribute disableQueryStringAttribute)
        {
            using (CodeTimingSessionManager.Current.Measure("Parse query string"))
            {
                DisableQueryStringAttribute disableQueryStringAttributeNotNull = disableQueryStringAttribute ?? DisableQueryStringAttribute.Empty;

                foreach ((string parameterName, StringValues parameterValue) in _queryStringAccessor.Query)
                {
                    IQueryStringParameterReader reader = _parameterReaders.FirstOrDefault(nextReader => nextReader.CanRead(parameterName));

                    if (reader != null)
                    {
                        _logger.LogDebug($"Query string parameter '{parameterName}' with value '{parameterValue}' was accepted by {reader.GetType().Name}.");

                        if (!reader.AllowEmptyValue && string.IsNullOrEmpty(parameterValue))
                        {
                            throw new InvalidQueryStringParameterException(parameterName, "Missing query string parameter value.",
                                $"Missing value for '{parameterName}' query string parameter.");
                        }

                        if (!reader.IsEnabled(disableQueryStringAttributeNotNull))
                        {
                            throw new InvalidQueryStringParameterException(parameterName,
                                "Usage of one or more query string parameters is not allowed at the requested endpoint.",
                                $"The parameter '{parameterName}' cannot be used at this endpoint.");
                        }

                        reader.Read(parameterName, parameterValue);
                        _logger.LogDebug($"Query string parameter '{parameterName}' was successfully read.");
                    }
                    else if (!_options.AllowUnknownQueryStringParameters)
                    {
                        throw new InvalidQueryStringParameterException(parameterName, "Unknown query string parameter.",
                            $"Query string parameter '{parameterName}' is unknown. " +
                            $"Set '{nameof(IJsonApiOptions.AllowUnknownQueryStringParameters)}' to 'true' in options to ignore unknown parameters.");
                    }
                }
            }
        }
    }
}
