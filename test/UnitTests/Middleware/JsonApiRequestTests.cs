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
        [InlineData("HEAD", "/todoItems", true, EndpointKind.Primary, true)]
        [InlineData("HEAD", "/todoItems/1", false, EndpointKind.Primary, true)]
        [InlineData("HEAD", "/todoItems/1/owner", false, EndpointKind.Secondary, true)]
        [InlineData("HEAD", "/todoItems/1/tags", true, EndpointKind.Secondary, true)]
        [InlineData("HEAD", "/todoItems/1/relationships/owner", false, EndpointKind.Relationship, true)]
        [InlineData("HEAD", "/todoItems/1/relationships/tags", true, EndpointKind.Relationship, true)]
        [InlineData("GET", "/todoItems", true, EndpointKind.Primary, true)]
        [InlineData("GET", "/todoItems/1", false, EndpointKind.Primary, true)]
        [InlineData("GET", "/todoItems/1/owner", false, EndpointKind.Secondary, true)]
        [InlineData("GET", "/todoItems/1/tags", true, EndpointKind.Secondary, true)]
        [InlineData("GET", "/todoItems/1/relationships/owner", false, EndpointKind.Relationship, true)]
        [InlineData("GET", "/todoItems/1/relationships/tags", true, EndpointKind.Relationship, true)]
        [InlineData("POST", "/todoItems", false, EndpointKind.Primary, false)]
        [InlineData("POST", "/todoItems/1/relationships/tags", true, EndpointKind.Relationship, false)]
        [InlineData("PATCH", "/todoItems/1", false, EndpointKind.Primary, false)]
        [InlineData("PATCH", "/todoItems/1/relationships/owner", false, EndpointKind.Relationship, false)]
        [InlineData("PATCH", "/todoItems/1/relationships/tags", true, EndpointKind.Relationship, false)]
        [InlineData("DELETE", "/todoItems/1", false, EndpointKind.Primary, false)]
        [InlineData("DELETE", "/todoItems/1/relationships/tags", true, EndpointKind.Relationship, false)]
        public async Task Sets_request_properties_correctly(string requestMethod, string requestPath, bool expectIsCollection, EndpointKind expectKind,
            bool expectIsReadOnly)
        {
            // Arrange
            var options = new JsonApiOptions
            {
                UseRelativeLinks = true
            };

            var graphBuilder = new ResourceGraphBuilder(options, NullLoggerFactory.Instance);
            graphBuilder.Add<TodoItem>();
            graphBuilder.Add<Person>();

            IResourceGraph resourceGraph = graphBuilder.Build();

            var controllerResourceMappingMock = new Mock<IControllerResourceMapping>();

            controllerResourceMappingMock.Setup(mapping => mapping.GetResourceTypeForController(It.IsAny<Type>())).Returns(typeof(TodoItem));

            var httpContext = new DefaultHttpContext();
            SetupRoutes(httpContext, requestMethod, requestPath);

            var request = new JsonApiRequest();

            var middleware = new JsonApiMiddleware(_ => Task.CompletedTask);

            // Act
            await middleware.InvokeAsync(httpContext, controllerResourceMappingMock.Object, options, request, resourceGraph);

            // Assert
            request.IsCollection.Should().Be(expectIsCollection);
            request.Kind.Should().Be(expectKind);
            request.IsReadOnly.Should().Be(expectIsReadOnly);
            request.BasePath.Should().BeEmpty();
            request.PrimaryResource.Should().NotBeNull();
            request.PrimaryResource.PublicName.Should().Be("todoItems");
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

            string[] pathSegments = requestPath.Split("/", StringSplitOptions.RemoveEmptyEntries);

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

            httpContext.Features.Set<IRouteValuesFeature>(feature);

            var controllerActionDescriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = (TypeInfo)typeof(object)
            };

            httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(controllerActionDescriptor), null));
        }
    }
}
