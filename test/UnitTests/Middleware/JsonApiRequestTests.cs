using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Middleware
{
    public sealed class JsonApiRequestTests
    {
        [Theory]
        [InlineData("GET", "/articles", true, EndpointKind.Primary, true)]
        [InlineData("GET", "/articles/1", false, EndpointKind.Primary, true)]
        [InlineData("GET", "/articles/1/author", false, EndpointKind.Secondary, true)]
        [InlineData("GET", "/articles/1/revisions", true, EndpointKind.Secondary, true)]
        [InlineData("GET", "/articles/1/relationships/author", false, EndpointKind.Relationship, true)]
        [InlineData("GET", "/articles/1/relationships/revisions", true, EndpointKind.Relationship, true)]
        [InlineData("POST", "/articles", false, EndpointKind.Primary, false)]
        [InlineData("POST", "/articles/1/relationships/revisions", true, EndpointKind.Relationship, false)]
        [InlineData("PATCH", "/articles/1", false, EndpointKind.Primary, false)]
        [InlineData("PATCH", "/articles/1/relationships/author", false, EndpointKind.Relationship, false)]
        [InlineData("PATCH", "/articles/1/relationships/revisions", true, EndpointKind.Relationship, false)]
        [InlineData("DELETE", "/articles/1", false, EndpointKind.Primary, false)]
        [InlineData("DELETE", "/articles/1/relationships/revisions", true, EndpointKind.Relationship, false)]
        public async Task Sets_request_properties_correctly(string requestMethod, string requestPath, bool expectIsCollection, EndpointKind expectKind, bool expectIsReadOnly)
        {
            // Arrange
            var options = new JsonApiOptions
            {
                UseRelativeLinks = true
            };

            var graphBuilder = new ResourceGraphBuilder(options, NullLoggerFactory.Instance);
            graphBuilder.Add<Article>();
            graphBuilder.Add<Author>();
            graphBuilder.Add<Revision>();

            var resourceGraph = graphBuilder.Build();

            var controllerResourceMappingMock = new Mock<IControllerResourceMapping>();

            controllerResourceMappingMock
                .Setup(x => x.GetResourceForEndpoint(It.IsAny<string>()))
                .Returns(typeof(Article));

            var httpContext = new DefaultHttpContext();
            SetupRoutes(httpContext, requestMethod, requestPath);

            var request = new JsonApiRequest();

            var middleware = new JsonApiMiddleware(_ => Task.CompletedTask);

            // Act
            await middleware.Invoke(httpContext, controllerResourceMappingMock.Object, options, request, resourceGraph);

            // Assert
            request.IsCollection.Should().Be(expectIsCollection);
            request.Kind.Should().Be(expectKind);
            request.IsReadOnly.Should().Be(expectIsReadOnly);
            request.BasePath.Should().BeEmpty();
            request.PrimaryResource.Should().NotBeNull();
            request.PrimaryResource.PublicName.Should().Be("articles");
        }

        private static void SetupRoutes(HttpContext httpContext, string requestMethod, string requestPath)
        {
            httpContext.Request.Method = requestMethod;

            var feature = new RouteValuesFeature
            {
                RouteValues =
                {
                    ["controller"] = "theController",
                    ["action"] = "theAction"
                }
            };

            var pathSegments = requestPath.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length > 1)
            {
                feature.RouteValues["id"] = pathSegments[1];

                if (pathSegments.Length >= 3)
                {
                    feature.RouteValues["relationshipName"] = pathSegments.Last();
                }
            }

            if (pathSegments.Contains("relationships"))
            {
                feature.RouteValues["action"] = "Relationship";
            }


            var endpointMetadata = new EndpointMetadataCollection(new ControllerActionDescriptor {ControllerTypeInfo = typeof(object).GetTypeInfo()});
            httpContext.Features.Set<IRouteValuesFeature>(feature);
            httpContext.SetEndpoint(new Endpoint(null, endpointMetadata, null));
        }
    }
}
