using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Moq;
using Xunit;

namespace UnitTests.Middleware
{
    public sealed class JsonApiMiddlewareTests
    {
        [Fact]
        public async Task ParseUrlBase_ObfuscatedIdClass_ShouldSetIdCorrectly()
        {
            // Arrange
            var id = "ABC123ABC";
            var configuration = GetConfiguration($"/obfuscatedIdModel/{id}", action: "GetAsync", id: id);
            var request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Equal(id, request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasPrimaryIdSet_ShouldSetupRequestWithSameId()
        {
            // Arrange
            var id = "123";
            var configuration = GetConfiguration($"/users/{id}", id: id);
            var request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Equal(id, request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasNoPrimaryIdSet_ShouldHaveBaseIdSetToNull()
        {
            // Arrange
            var configuration = GetConfiguration("/users");
            var request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Null(request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasNegativePrimaryIdAndTypeIsInt_ShouldNotThrowJAException()
        {
            // Arrange
            var configuration = GetConfiguration("/users/-5/");

            // Act / Assert
            await RunMiddlewareTask(configuration);
        }

        private sealed class InvokeConfiguration
        {
            public JsonApiMiddleware MiddleWare;
            public HttpContext HttpContext;
            public Mock<IControllerResourceMapping> ControllerResourceMapping;
            public Mock<IJsonApiOptions> Options;
            public JsonApiRequest Request;
            public Mock<IResourceGraph> ResourceGraph;
        }
        private Task RunMiddlewareTask(InvokeConfiguration holder)
        {
            var controllerResourceMapping = holder.ControllerResourceMapping.Object;
            var context = holder.HttpContext;
            var endpointMetadata = new EndpointMetadataCollection(new ControllerActionDescriptor {ControllerTypeInfo = typeof(object).GetTypeInfo()});
            context.SetEndpoint(new Endpoint(null, endpointMetadata, null));
            var options = holder.Options.Object;
            var request = holder.Request;
            var resourceGraph = holder.ResourceGraph.Object;
            return holder.MiddleWare.Invoke(context, controllerResourceMapping, options, request, resourceGraph);
        }
        private InvokeConfiguration GetConfiguration(string path, string resourceName = "users", string action = "", string id =null, Type relType = null)
        {
            if (path.First() != '/')
            {
                throw new ArgumentException("Path should start with a '/'");
            }
            var middleware = new JsonApiMiddleware(httpContext =>
            {
                return Task.Run(() => Console.WriteLine("finished"));
            });
            var forcedNamespace = "api/v1";
            var mockMapping = new Mock<IControllerResourceMapping>();
            mockMapping.Setup(x => x.GetResourceForEndpoint(It.IsAny<string>())).Returns(typeof(string));

            Mock<IJsonApiOptions> mockOptions = CreateMockOptions(forcedNamespace);
            var mockGraph = CreateMockResourceGraph(resourceName, includeRelationship: relType != null);
            var request = new JsonApiRequest();
            if (relType != null)
            {
                request.Relationship = new HasManyAttribute
                {
                    RightType = relType
                };
            }
            var context = CreateHttpContext(path, isRelationship: relType != null, action: action, id: id);
            return new InvokeConfiguration
            {
                MiddleWare = middleware,
                ControllerResourceMapping = mockMapping,
                Options = mockOptions,
                Request = request,
                HttpContext = context,
                ResourceGraph = mockGraph
            };
        }

        private static Mock<IJsonApiOptions> CreateMockOptions(string forcedNamespace)
        {
            var mockOptions = new Mock<IJsonApiOptions>();
            mockOptions.Setup(o => o.Namespace).Returns(forcedNamespace);
            return mockOptions;
        }

        private static DefaultHttpContext CreateHttpContext(string path, bool isRelationship = false, string action = "", string id =null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString(path);
            context.Response.Body = new MemoryStream();
            var feature = new RouteValuesFeature
            {
                RouteValues = {["controller"] = "fake!", ["action"] = isRelationship ? "GetRelationship" : action}
            };
            if(id != null)
            {
                feature.RouteValues["id"] = id;
            }
            context.Features.Set<IRouteValuesFeature>(feature);
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(), null));
            return context;
        }

        private Mock<IResourceGraph> CreateMockResourceGraph( string resourceName, bool includeRelationship = false)
        {
            var mockGraph = new Mock<IResourceGraph>();
            var resourceContext = new ResourceContext
            {
                PublicName = resourceName,
                IdentityType = typeof(string)
            };
             var seq = mockGraph.SetupSequence(d => d.GetResourceContext(It.IsAny<Type>())).Returns(resourceContext);
            if (includeRelationship)
            {
                var relResourceContext = new ResourceContext
                {
                    PublicName = "todoItems",
                    IdentityType = typeof(string)
                };
                seq.Returns(relResourceContext);
            }
            return mockGraph;
        }

    }
}
