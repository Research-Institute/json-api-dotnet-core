using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JsonApiDotNetCore.Models.JsonApiDocuments
{
    public class ErrorDocument
    {
        public IList<Error> Errors { get; }

        public ErrorDocument()
        {
            Errors = new List<Error>();
        }

        public ErrorDocument(Error error)
        {
            Errors = new List<Error>
            {
                error
            };
        }

        public ErrorDocument(IList<Error> errors)
        {
            Errors = errors;
        }

        public string GetJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
        }

        public HttpStatusCode GetErrorStatusCode()
        {
            var statusCodes = Errors
                .Select(e => (int)e.Status)
                .Distinct()
                .ToList();

            if (statusCodes.Count == 1)
                return (HttpStatusCode)statusCodes[0];

            var statusCode = int.Parse(statusCodes.Max().ToString()[0] + "00");
            return (HttpStatusCode)statusCode;
        }

        public IActionResult AsActionResult()
        {
            return new ObjectResult(this)
            {
                StatusCode = (int)GetErrorStatusCode()
            };
        }
    }
}