using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.QueryStrings;

namespace JsonApiDotNetCore.Controllers.Annotations
{
    /// <summary>
    /// Used on an ASP.NET Core controller class to indicate which query string parameters are blocked.
    /// </summary>
    /// <example><![CDATA[
    /// [DisableQueryString(StandardQueryStringParameters.Sort | StandardQueryStringParameters.Page)]
    /// public class CustomersController : JsonApiController<Customer> { }
    /// ]]></example>
    /// <example><![CDATA[
    /// [DisableQueryString("skipCache")]
    /// public class CustomersController : JsonApiController<Customer> { }
    /// ]]></example>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class DisableQueryStringAttribute : Attribute
    {
        public static readonly DisableQueryStringAttribute Empty = new(StandardQueryStringParameters.None);
        public IReadOnlyCollection<string> ParameterNames { get; }

        /// <summary>
        /// Disables one or more of the builtin query parameters for a controller.
        /// </summary>
        public DisableQueryStringAttribute(StandardQueryStringParameters parameters)
        {
            var parameterNames = new List<string>();

            foreach (StandardQueryStringParameters value in Enum.GetValues(typeof(StandardQueryStringParameters)))
            {
                if (value != StandardQueryStringParameters.None && value != StandardQueryStringParameters.All && parameters.HasFlag(value))
                {
                    parameterNames.Add(value.ToString());
                }
            }

            ParameterNames = parameterNames;
        }

        /// <summary>
        /// It is allowed to use a comma-separated list of strings to indicate which query parameters should be disabled, because the user may have defined
        /// custom query parameters that are not included in the <see cref="StandardQueryStringParameters" /> enum.
        /// </summary>
        public DisableQueryStringAttribute(string parameterNames)
        {
            ArgumentGuard.NotNullNorEmpty(parameterNames, nameof(parameterNames));

            ParameterNames = parameterNames.Split(",").ToList();
        }

        public bool ContainsParameter(StandardQueryStringParameters parameter)
        {
            string name = parameter.ToString();
            return ParameterNames.Contains(name);
        }
    }
}
