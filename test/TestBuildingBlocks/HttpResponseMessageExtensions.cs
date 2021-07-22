using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Primitives;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuildingBlocks
{
    [PublicAPI]
    public static class HttpResponseMessageExtensions
    {
        public static HttpResponseMessageAssertions Should(this HttpResponseMessage instance)
        {
            return new(instance);
        }

        public sealed class HttpResponseMessageAssertions : ReferenceTypeAssertions<HttpResponseMessage, HttpResponseMessageAssertions>
        {
            protected override string Identifier => "response";

            public HttpResponseMessageAssertions(HttpResponseMessage instance)
            {
                Subject = instance;
            }

            // ReSharper disable once UnusedMethodReturnValue.Global
            [CustomAssertion]
            public AndConstraint<HttpResponseMessageAssertions> HaveStatusCode(HttpStatusCode statusCode)
            {
                if (Subject.StatusCode != statusCode)
                {
                    string responseText = GetFormattedContentAsync(Subject).Result;
                    Subject.StatusCode.Should().Be(statusCode, "response body returned was:\n" + responseText);
                }

                return new AndConstraint<HttpResponseMessageAssertions>(this);
            }

            private static async Task<string> GetFormattedContentAsync(HttpResponseMessage responseMessage)
            {
                string text = await responseMessage.Content.ReadAsStringAsync();

                try
                {
                    if (text.Length > 0)
                    {
                        return JsonConvert.DeserializeObject<JObject>(text)?.ToString();
                    }
                }
#pragma warning disable AV1210 // Catch a specific exception instead of Exception, SystemException or ApplicationException
                catch
#pragma warning restore AV1210 // Catch a specific exception instead of Exception, SystemException or ApplicationException
                {
                    // ignored
                }

                return text;
            }
        }
    }
}
