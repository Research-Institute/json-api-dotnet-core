using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCore.Configuration
{
    [PublicAPI]
    public static class ServiceCollectionExtensions
    {
        private static readonly TypeLocator TypeLocator = new();

        /// <summary>
        /// Configures JsonApiDotNetCore by registering resources manually.
        /// </summary>
        public static IServiceCollection AddJsonApi(this IServiceCollection services, Action<JsonApiOptions> options = null,
            Action<ServiceDiscoveryFacade> discovery = null, Action<ResourceGraphBuilder> resources = null, IMvcCoreBuilder mvcBuilder = null,
            ICollection<Type> dbContextTypes = null)
        {
            ArgumentGuard.NotNull(services, nameof(services));

            SetupApplicationBuilder(services, options, discovery, resources, mvcBuilder, dbContextTypes ?? Array.Empty<Type>());

            return services;
        }

        /// <summary>
        /// Configures JsonApiDotNetCore by registering resources from an Entity Framework Core model.
        /// </summary>
        public static IServiceCollection AddJsonApi<TDbContext>(this IServiceCollection services, Action<JsonApiOptions> options = null,
            Action<ServiceDiscoveryFacade> discovery = null, Action<ResourceGraphBuilder> resources = null, IMvcCoreBuilder mvcBuilder = null)
            where TDbContext : DbContext
        {
            return AddJsonApi(services, options, discovery, resources, mvcBuilder, typeof(TDbContext).AsArray());
        }

        private static void SetupApplicationBuilder(IServiceCollection services, Action<JsonApiOptions> configureOptions,
            Action<ServiceDiscoveryFacade> configureAutoDiscovery, Action<ResourceGraphBuilder> configureResourceGraph, IMvcCoreBuilder mvcBuilder,
            ICollection<Type> dbContextTypes)
        {
            using var applicationBuilder = new JsonApiApplicationBuilder(services, mvcBuilder ?? services.AddMvcCore());

            applicationBuilder.ConfigureJsonApiOptions(configureOptions);
            applicationBuilder.ConfigureAutoDiscovery(configureAutoDiscovery);
            applicationBuilder.AddResourceGraph(dbContextTypes, configureResourceGraph);
            applicationBuilder.ConfigureMvc();
            applicationBuilder.DiscoverInjectables();
            applicationBuilder.ConfigureServiceContainer(dbContextTypes);
        }

        /// <summary>
        /// Enables client serializers for sending requests and receiving responses in JSON:API format. Internally only used for testing. Will be extended in the
        /// future to be part of a JsonApiClientDotNetCore package.
        /// </summary>
        public static IServiceCollection AddClientSerialization(this IServiceCollection services)
        {
            ArgumentGuard.NotNull(services, nameof(services));

            services.AddScoped<IResponseDeserializer, ResponseDeserializer>();

            services.AddScoped<IRequestSerializer>(sp =>
            {
                var graph = sp.GetRequiredService<IResourceGraph>();
                return new RequestSerializer(graph, new ResourceObjectBuilder(graph, new ResourceObjectBuilderSettings()));
            });

            return services;
        }

        /// <summary>
        /// Adds IoC container registrations for the various JsonApiDotNetCore resource service interfaces, such as <see cref="IGetAllService{TResource}" />,
        /// <see cref="ICreateService{TResource}" /> and the various others.
        /// </summary>
        public static IServiceCollection AddResourceService<TService>(this IServiceCollection services)
        {
            ArgumentGuard.NotNull(services, nameof(services));

            RegisterForConstructedType(services, typeof(TService), ServiceDiscoveryFacade.ServiceInterfaces);

            return services;
        }

        /// <summary>
        /// Adds IoC container registrations for the various JsonApiDotNetCore resource repository interfaces, such as
        /// <see cref="IResourceReadRepository{TResource}" /> and <see cref="IResourceWriteRepository{TResource}" />.
        /// </summary>
        public static IServiceCollection AddResourceRepository<TRepository>(this IServiceCollection services)
        {
            ArgumentGuard.NotNull(services, nameof(services));

            RegisterForConstructedType(services, typeof(TRepository), ServiceDiscoveryFacade.RepositoryInterfaces);

            return services;
        }

        /// <summary>
        /// Adds IoC container registrations for the various JsonApiDotNetCore resource definition interfaces, such as
        /// <see cref="IResourceDefinition{TResource}" /> and <see cref="IResourceDefinition{TResource,TId}" />.
        /// </summary>
        public static IServiceCollection AddResourceDefinition<TResourceDefinition>(this IServiceCollection services)
        {
            ArgumentGuard.NotNull(services, nameof(services));

            RegisterForConstructedType(services, typeof(TResourceDefinition), ServiceDiscoveryFacade.ResourceDefinitionInterfaces);

            return services;
        }

        private static void RegisterForConstructedType(IServiceCollection services, Type implementationType, IEnumerable<Type> openGenericInterfaces)
        {
            bool seenCompatibleInterface = false;
            ResourceDescriptor resourceDescriptor = TryGetResourceTypeFromServiceImplementation(implementationType);

            if (resourceDescriptor != null)
            {
                foreach (Type openGenericInterface in openGenericInterfaces)
                {
                    // A shorthand interface is one where the ID type is omitted.
                    // e.g. IResourceService<TResource> is the shorthand for IResourceService<TResource, TId>
                    bool isShorthandInterface = openGenericInterface.GetTypeInfo().GenericTypeParameters.Length == 1;

                    if (isShorthandInterface && resourceDescriptor.IdType != typeof(int))
                    {
                        // We can't create a shorthand for ID types other than int.
                        continue;
                    }

                    Type constructedType = isShorthandInterface
                        ? openGenericInterface.MakeGenericType(resourceDescriptor.ResourceType)
                        : openGenericInterface.MakeGenericType(resourceDescriptor.ResourceType, resourceDescriptor.IdType);

                    if (constructedType.IsAssignableFrom(implementationType))
                    {
                        services.AddScoped(constructedType, implementationType);
                        seenCompatibleInterface = true;
                    }
                }
            }

            if (!seenCompatibleInterface)
            {
                throw new InvalidConfigurationException($"{implementationType} does not implement any of the expected JsonApiDotNetCore interfaces.");
            }
        }

        private static ResourceDescriptor TryGetResourceTypeFromServiceImplementation(Type serviceType)
        {
            foreach (Type @interface in serviceType.GetInterfaces())
            {
                Type firstGenericArgument = @interface.IsGenericType ? @interface.GenericTypeArguments.First() : null;

                if (firstGenericArgument != null)
                {
                    ResourceDescriptor resourceDescriptor = TypeLocator.TryGetResourceDescriptor(firstGenericArgument);

                    if (resourceDescriptor != null)
                    {
                        return resourceDescriptor;
                    }
                }
            }

            return null;
        }
    }
}
