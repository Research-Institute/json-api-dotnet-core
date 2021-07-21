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
using Moq.Language;
using Xunit;

namespace UnitTests.Middleware
{
    public sealed class JsonApiMiddlewareTests
    {
        [Fact]
        public async Task ParseUrlBase_ObfuscatedIdClass_ShouldSetIdCorrectly()
        {
            // Arrange
            const string id = "ABC123ABC";
            InvokeConfiguration configuration = GetConfiguration($"/obfuscatedIdModel/{id}", action: "GetAsync", id: id);
            JsonApiRequest request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Equal(id, request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasPrimaryIdSet_ShouldSetupRequestWithSameId()
        {
            // Arrange
            const string id = "123";
            InvokeConfiguration configuration = GetConfiguration($"/users/{id}", id: id);
            JsonApiRequest request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Equal(id, request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasNoPrimaryIdSet_ShouldHaveBaseIdSetToNull()
        {
            // Arrange
            InvokeConfiguration configuration = GetConfiguration("/users");
            JsonApiRequest request = configuration.Request;

            // Act
            await RunMiddlewareTask(configuration);

            // Assert
            Assert.Null(request.PrimaryId);
        }

        [Fact]
        public async Task ParseUrlBase_UrlHasNegativePrimaryIdAndTypeIsInt_ShouldNotThrowJAException()
        {
            // Arrange
            InvokeConfiguration configuration = GetConfiguration("/users/-5/");

            // Act
            Func<Task> asyncAction = async () => await RunMiddlewareTask(configuration);

            // Assert
            await asyncAction();
        }

        private Task RunMiddlewareTask(InvokeConfiguration holder)
        {
            IControllerResourceMapping controllerResourceMapping = holder.ControllerResourceMapping.Object;
            HttpContext context = holder.HttpContext;
            IJsonApiOptions options = holder.Options.Object;
            JsonApiRequest request = holder.Request;
            IResourceGraph resourceGraph = holder.ResourceGraph.Object;
            return holder.MiddleWare.InvokeAsync(context, controllerResourceMapping, options, request, resourceGraph);
        }

        private InvokeConfiguration GetConfiguration(string path, string resourceName = "users", string action = "", string id = null, Type relType = null)
        {
            if (path.First() != '/')
            {
                throw new ArgumentException("Path should start with a '/'");
            }

            var middleware = new JsonApiMiddleware(_ =>
            {
                return Task.Run(() => Console.WriteLine("finished"));
            });

            const string forcedNamespace = "api/v1";
            var mockMapping = new Mock<IControllerResourceMapping>();
            mockMapping.Setup(mapping => mapping.GetResourceTypeForController(It.IsAny<Type>())).Returns(typeof(string));

            Mock<IJsonApiOptions> mockOptions = CreateMockOptions(forcedNamespace);
            Mock<IResourceGraph> mockGraph = CreateMockResourceGraph(resourceName, relType != null);
            var request = new JsonApiRequest();

            if (relType != null)
            {
                request.Relationship = new HasManyAttribute
                {
                    RightType = relType
                };
            }

            DefaultHttpContext context = CreateHttpContext(path, relType != null, action, id);

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
            mockOptions.Setup(options => options.Namespace).Returns(forcedNamespace);
            return mockOptions;
        }

        private static DefaultHttpContext CreateHttpContext(string path, bool isRelationship = false, string action = "", string id = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString(path);
            context.Response.Body = new MemoryStream();

            var feature = new RouteValuesFeature
            {
                RouteValues =
                {
                    ["controller"] = "fake!",
                    ["action"] = isRelationship ? "GetRelationship" : action
                }
            };

            if (id != null)
            {
                feature.RouteValues["id"] = id;
            }

            context.Features.Set<IRouteValuesFeature>(feature);

            var controllerActionDescriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = (TypeInfo)typeof(object)
            };

            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(controllerActionDescriptor), null));
            return context;
        }

        private Mock<IResourceGraph> CreateMockResourceGraph(string resourceName, bool includeRelationship = false)
        {
            var mockGraph = new Mock<IResourceGraph>();

            var resourceContext = new ResourceContext
            {
                PublicName = resourceName,
                IdentityType = typeof(string)
            };

            ISetupSequentialResult<ResourceContext> seq = mockGraph.SetupSequence(resourceGraph => resourceGraph.GetResourceContext(It.IsAny<Type>()))
                .Returns(resourceContext);

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

        private sealed class InvokeConfiguration
        {
            public JsonApiMiddleware MiddleWare { get; init; }
            public HttpContext HttpContext { get; init; }
            public Mock<IControllerResourceMapping> ControllerResourceMapping { get; init; }
            public Mock<IJsonApiOptions> Options { get; init; }
            public JsonApiRequest Request { get; init; }
            public Mock<IResourceGraph> ResourceGraph { get; init; }
        }
    }
}
