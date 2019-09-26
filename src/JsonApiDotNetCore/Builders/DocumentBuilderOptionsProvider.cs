using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;

namespace JsonApiDotNetCore.Builders
{
    public class DocumentBuilderOptionsProvider : IDocumentBuilderOptionsProvider
    {
        private readonly IJsonApiContext _jsonApiContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DocumentBuilderOptionsProvider(IJsonApiOptions options, IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public SerializerBehaviour GetDocumentBuilderOptions()
        {
            var nullAttributeResponseBehaviorConfig = this._jsonApiContext.Options.NullAttributeResponseBehavior;
            if (nullAttributeResponseBehaviorConfig.AllowClientOverride && _httpContextAccessor.HttpContext.Request.Query.TryGetValue("omitNullValuedAttributes", out var omitNullValuedAttributesQs))
            {
                if (bool.TryParse(omitNullValuedAttributesQs, out var omitNullValuedAttributes))
                {
                    //return new SerializerBehaviour(omitNullValuedAttributes);
                    return null;
                }
            }
            //return new SerializerBehaviour(this._jsonApiContext.Options.NullAttributeResponseBehavior.OmitNullValuedAttributes);

            return null;
        }
    }

    public interface IDocumentBuilderOptionsProvider
    {
    }
}
