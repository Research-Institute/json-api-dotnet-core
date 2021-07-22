using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCoreExample.Startups;
using Microsoft.AspNetCore.Mvc.Testing;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.NonJsonApiControllers
{
    public sealed class NonJsonApiControllerTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public NonJsonApiControllerTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Get_skips_middleware_and_formatters()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Get, "/NonJsonApi");

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("application/json; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("[\"Welcome!\"]");
        }

        [Fact]
        public async Task Post_skips_middleware_and_formatters()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Post, "/NonJsonApi")
            {
                Content = new StringContent("Jack")
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("text/plain")
                    }
                }
            };

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("text/plain; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("Hello, Jack");
        }

        [Fact]
        public async Task Post_skips_error_handler()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Post, "/NonJsonApi");

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("text/plain; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("Please send your name.");
        }

        [Fact]
        public async Task Put_skips_middleware_and_formatters()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Put, "/NonJsonApi")
            {
                Content = new StringContent("\"Jane\"")
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("text/plain; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("Hi, Jane");
        }

        [Fact]
        public async Task Patch_skips_middleware_and_formatters()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/NonJsonApi?name=Janice");

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("text/plain; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("Good day, Janice");
        }

        [Fact]
        public async Task Delete_skips_middleware_and_formatters()
        {
            // Arrange
            using var request = new HttpRequestMessage(HttpMethod.Delete, "/NonJsonApi");

            HttpClient client = _factory.CreateClient();

            // Act
            HttpResponseMessage httpResponse = await client.SendAsync(request);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
            httpResponse.Content.Headers.ContentType.Should().NotBeNull().And.Subject.ToString().Should().Be("text/plain; charset=utf-8");

            string responseText = await httpResponse.Content.ReadAsStringAsync();
            responseText.Should().Be("Bye.");
        }
    }
}
