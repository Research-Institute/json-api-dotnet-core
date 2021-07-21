using System;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Serialization.Objects
{
    /// <summary>
    /// Provides additional information about a problem encountered while performing an operation. Error objects MUST be returned as an array keyed by errors
    /// in the top level of a JSON:API document.
    /// </summary>
    [PublicAPI]
    public sealed class Error
    {
        /// <summary>
        /// A unique identifier for this particular occurrence of the problem.
        /// </summary>
        [JsonProperty]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// A link that leads to further details about this particular occurrence of the problem.
        /// </summary>
        [JsonProperty]
        public ErrorLinks Links { get; set; } = new();

        /// <summary>
        /// The HTTP status code applicable to this problem.
        /// </summary>
        [JsonIgnore]
        public HttpStatusCode StatusCode { get; set; }

        [JsonProperty]
        public string Status
        {
            get => StatusCode.ToString("d");
            set => StatusCode = (HttpStatusCode)int.Parse(value);
        }

        /// <summary>
        /// An application-specific error code.
        /// </summary>
        [JsonProperty]
        public string Code { get; set; }

        /// <summary>
        /// A short, human-readable summary of the problem that SHOULD NOT change from occurrence to occurrence of the problem, except for purposes of
        /// localization.
        /// </summary>
        [JsonProperty]
        public string Title { get; set; }

        /// <summary>
        /// A human-readable explanation specific to this occurrence of the problem. Like title, this field's value can be localized.
        /// </summary>
        [JsonProperty]
        public string Detail { get; set; }

        /// <summary>
        /// An object containing references to the source of the error.
        /// </summary>
        [JsonProperty]
        public ErrorSource Source { get; set; } = new();

        /// <summary>
        /// An object containing non-standard meta-information (key/value pairs) about the error.
        /// </summary>
        [JsonProperty]
        public ErrorMeta Meta { get; set; } = new();

        public Error(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public bool ShouldSerializeLinks()
        {
            return Links?.About != null;
        }

        public bool ShouldSerializeSource()
        {
            return Source != null && (Source.Pointer != null || Source.Parameter != null);
        }

        public bool ShouldSerializeMeta()
        {
            return Meta != null && Meta.Data.Any();
        }
    }
}
