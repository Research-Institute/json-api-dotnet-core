using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Replacement implementation for the ASP.NET built-in <see cref="QueryFeature" />, to workaround bug https://github.com/dotnet/aspnetcore/issues/33394.
    /// This is identical to the built-in version, except it calls <see cref="FixedQueryHelpers.ParseNullableQuery" />.
    /// </summary>
    internal sealed class FixedQueryFeature : IQueryFeature
    {
        // Lambda hoisted to static readonly field to improve inlining https://github.com/dotnet/roslyn/issues/13624
        private static readonly Func<IFeatureCollection, IHttpRequestFeature> NullRequestFeature = _ => null;

        private FeatureReferences<IHttpRequestFeature> _features;

        private string _original;
        private IQueryCollection _parsedValues;

        private IHttpRequestFeature HttpRequestFeature => _features.Fetch(ref _features.Cache, NullRequestFeature);

        /// <inheritdoc />
        public IQueryCollection Query
        {
            get
            {
                if (IsFeatureCollectionNull())
                {
                    return _parsedValues ??= QueryCollection.Empty;
                }

                string current = HttpRequestFeature.QueryString;

                if (_parsedValues == null || !string.Equals(_original, current, StringComparison.Ordinal))
                {
                    _original = current;

                    Dictionary<string, StringValues> result = FixedQueryHelpers.ParseNullableQuery(current);

                    _parsedValues = result == null ? QueryCollection.Empty : new QueryCollection(result);
                }

                return _parsedValues;
            }
            set
            {
                _parsedValues = value;

                if (!IsFeatureCollectionNull())
                {
                    if (value == null)
                    {
                        _original = string.Empty;
                        HttpRequestFeature.QueryString = string.Empty;
                    }
                    else
                    {
                        _original = QueryString.Create(_parsedValues).ToString();
                        HttpRequestFeature.QueryString = _original;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="QueryFeature" />.
        /// </summary>
        /// <param name="features">
        /// The <see cref="IFeatureCollection" /> to initialize.
        /// </param>
        public FixedQueryFeature(IFeatureCollection features)
        {
            ArgumentGuard.NotNull(features, nameof(features));

            _features.Initalize(features);
        }

        private bool IsFeatureCollectionNull()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // Justification: This code was copied from the ASP.NET sources. A struct instance can be created without calling one of its constructors.
            return _features.Collection == null;
        }
    }
}
