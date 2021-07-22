using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiscoveryTests
{
    public sealed class ServiceDiscoveryFacadeTests
    {
        private static readonly NullLoggerFactory LoggerFactory = NullLoggerFactory.Instance;
        private readonly IServiceCollection _services = new ServiceCollection();
        private readonly JsonApiOptions _options = new();
        private readonly ResourceGraphBuilder _resourceGraphBuilder;

        public ServiceDiscoveryFacadeTests()
        {
            var dbResolverMock = new Mock<IDbContextResolver>();
            dbResolverMock.Setup(resolver => resolver.GetContext()).Returns(new Mock<DbContext>().Object);
            _services.AddScoped(_ => dbResolverMock.Object);

            _services.AddSingleton<IJsonApiOptions>(_options);
            _services.AddSingleton<ILoggerFactory>(LoggerFactory);
            _services.AddScoped(_ => new Mock<IJsonApiRequest>().Object);
            _services.AddScoped(_ => new Mock<ITargetedFields>().Object);
            _services.AddScoped(_ => new Mock<IResourceGraph>().Object);
            _services.AddScoped(_ => new Mock<IResourceContextProvider>().Object);
            _services.AddScoped(typeof(IResourceChangeTracker<>), typeof(ResourceChangeTracker<>));
            _services.AddScoped(_ => new Mock<IResourceFactory>().Object);
            _services.AddScoped(_ => new Mock<IPaginationContext>().Object);
            _services.AddScoped(_ => new Mock<IQueryLayerComposer>().Object);
            _services.AddScoped(_ => new Mock<IResourceRepositoryAccessor>().Object);

            _resourceGraphBuilder = new ResourceGraphBuilder(_options, LoggerFactory);
        }

        [Fact]
        public void Can_add_resources_from_assembly_to_graph()
        {
            // Arrange
            var facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, LoggerFactory);
            facade.AddAssembly(typeof(Person).Assembly);

            // Act
            facade.DiscoverResources();

            // Assert
            IResourceGraph resourceGraph = _resourceGraphBuilder.Build();

            ResourceContext personContext = resourceGraph.GetResourceContext(typeof(Person));
            personContext.Should().NotBeNull();

            ResourceContext todoItemContext = resourceGraph.GetResourceContext(typeof(TodoItem));
            todoItemContext.Should().NotBeNull();
        }

        [Fact]
        public void Can_add_resource_from_current_assembly_to_graph()
        {
            // Arrange
            var facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, LoggerFactory);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverResources();

            // Assert
            IResourceGraph resourceGraph = _resourceGraphBuilder.Build();

            ResourceContext testContext = resourceGraph.GetResourceContext(typeof(TestResource));
            testContext.Should().NotBeNull();
        }

        [Fact]
        public void Can_add_resource_service_from_current_assembly_to_container()
        {
            // Arrange
            var facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, LoggerFactory);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverInjectables();

            // Assert
            ServiceProvider services = _services.BuildServiceProvider();

            var resourceService = services.GetRequiredService<IResourceService<TestResource>>();
            resourceService.Should().BeOfType<TestResourceService>();
        }

        [Fact]
        public void Can_add_resource_repository_from_current_assembly_to_container()
        {
            // Arrange
            var facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, LoggerFactory);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverInjectables();

            // Assert
            ServiceProvider services = _services.BuildServiceProvider();

            var resourceRepository = services.GetRequiredService<IResourceRepository<TestResource>>();
            resourceRepository.Should().BeOfType<TestResourceRepository>();
        }

        [Fact]
        public void Can_add_resource_definition_from_current_assembly_to_container()
        {
            // Arrange
            var facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, LoggerFactory);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverInjectables();

            // Assert
            ServiceProvider services = _services.BuildServiceProvider();

            var resourceDefinition = services.GetRequiredService<IResourceDefinition<TestResource>>();
            resourceDefinition.Should().BeOfType<TestResourceDefinition>();
        }
    }
}
