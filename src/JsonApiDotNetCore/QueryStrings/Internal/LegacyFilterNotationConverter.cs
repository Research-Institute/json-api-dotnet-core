using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JsonApiDotNetCore.Queries.Internal.Parsing;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    [PublicAPI]
    public sealed class LegacyFilterNotationConverter
    {
        private const string ParameterNamePrefix = "filter[";
        private const string ParameterNameSuffix = "]";
        private const string OutputParameterName = "filter";

        private const string ExpressionPrefix = "expr:";
        private const string NotEqualsPrefix = "ne:";
        private const string InPrefix = "in:";
        private const string NotInPrefix = "nin:";

        private static readonly Dictionary<string, string> PrefixConversionTable = new()
        {
            ["eq:"] = Keywords.Equals,
            ["lt:"] = Keywords.LessThan,
            ["le:"] = Keywords.LessOrEqual,
            ["gt:"] = Keywords.GreaterThan,
            ["ge:"] = Keywords.GreaterOrEqual,
            ["like:"] = Keywords.Contains
        };

        public IEnumerable<string> ExtractConditions(string parameterValue)
        {
            ArgumentGuard.NotNullNorEmpty(parameterValue, nameof(parameterValue));

            if (parameterValue.StartsWith(ExpressionPrefix, StringComparison.Ordinal) || parameterValue.StartsWith(InPrefix, StringComparison.Ordinal) ||
                parameterValue.StartsWith(NotInPrefix, StringComparison.Ordinal))
            {
                yield return parameterValue;
            }
            else
            {
                foreach (string condition in parameterValue.Split(','))
                {
                    yield return condition;
                }
            }
        }

        public (string parameterName, string parameterValue) Convert(string parameterName, string parameterValue)
        {
            ArgumentGuard.NotNullNorEmpty(parameterName, nameof(parameterName));
            ArgumentGuard.NotNullNorEmpty(parameterValue, nameof(parameterValue));

            if (parameterValue.StartsWith(ExpressionPrefix, StringComparison.Ordinal))
            {
                string expression = parameterValue.Substring(ExpressionPrefix.Length);
                return (parameterName, expression);
            }

            string attributeName = ExtractAttributeName(parameterName);

            foreach ((string prefix, string keyword) in PrefixConversionTable)
            {
                if (parameterValue.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string value = parameterValue.Substring(prefix.Length);
                    string escapedValue = EscapeQuotes(value);
                    string expression = $"{keyword}({attributeName},'{escapedValue}')";

                    return (OutputParameterName, expression);
                }
            }

            if (parameterValue.StartsWith(NotEqualsPrefix, StringComparison.Ordinal))
            {
                string value = parameterValue.Substring(NotEqualsPrefix.Length);
                string escapedValue = EscapeQuotes(value);
                string expression = $"{Keywords.Not}({Keywords.Equals}({attributeName},'{escapedValue}'))";

                return (OutputParameterName, expression);
            }

            if (parameterValue.StartsWith(InPrefix, StringComparison.Ordinal))
            {
                string[] valueParts = parameterValue.Substring(InPrefix.Length).Split(",");
                string valueList = "'" + string.Join("','", valueParts) + "'";
                string expression = $"{Keywords.Any}({attributeName},{valueList})";

                return (OutputParameterName, expression);
            }

            if (parameterValue.StartsWith(NotInPrefix, StringComparison.Ordinal))
            {
                string[] valueParts = parameterValue.Substring(NotInPrefix.Length).Split(",");
                string valueList = "'" + string.Join("','", valueParts) + "'";
                string expression = $"{Keywords.Not}({Keywords.Any}({attributeName},{valueList}))";

                return (OutputParameterName, expression);
            }

            if (parameterValue == "isnull:")
            {
                string expression = $"{Keywords.Equals}({attributeName},null)";
                return (OutputParameterName, expression);
            }

            if (parameterValue == "isnotnull:")
            {
                string expression = $"{Keywords.Not}({Keywords.Equals}({attributeName},null))";
                return (OutputParameterName, expression);
            }

            {
                string escapedValue = EscapeQuotes(parameterValue);
                string expression = $"{Keywords.Equals}({attributeName},'{escapedValue}')";

                return (OutputParameterName, expression);
            }
        }

        private static string ExtractAttributeName(string parameterName)
        {
            if (parameterName.StartsWith(ParameterNamePrefix, StringComparison.Ordinal) &&
                parameterName.EndsWith(ParameterNameSuffix, StringComparison.Ordinal))
            {
                string attributeName = parameterName.Substring(ParameterNamePrefix.Length,
                    parameterName.Length - ParameterNamePrefix.Length - ParameterNameSuffix.Length);

                if (attributeName.Length > 0)
                {
                    return attributeName;
                }
            }

            throw new QueryParseException("Expected field name between brackets in filter parameter name.");
        }

        private static string EscapeQuotes(string text)
        {
            return text.Replace("'", "''");
        }
    }
}
