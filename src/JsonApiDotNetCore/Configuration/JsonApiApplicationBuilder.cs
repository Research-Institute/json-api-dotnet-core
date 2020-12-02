using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Hooks;
using JsonApiDotNetCore.Hooks.Internal;
using JsonApiDotNetCore.Hooks.Internal.Discovery;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Hooks.Internal.Traversal;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Internal;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.QueryStrings.Internal;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// A utility class that builds a JsonApi application. It registers all required services
    /// and allows the user to override parts of the startup configuration.
    /// </summary>
    internal sealed class JsonApiApplicationBuilder : IJsonApiApplicationBuilder, IDisposable
    {
        private readonly JsonApiOptions _options = new JsonApiOptions();
        private readonly IServiceCollection _services;
        private readonly IMvcCoreBuilder _mvcBuilder;
        private readonly ResourceGraphBuilder _resourceGraphBuilder;
        private readonly ServiceDiscoveryFacade _serviceDiscoveryFacade;
        private readonly ServiceProvider _intermediateProvider;
        private IResourceGraph _resourceGraph;

        public Action<MvcOptions> ConfigureMvcOptions { get; set; }

        public JsonApiApplicationBuilder(IServiceCollection services, IMvcCoreBuilder mvcBuilder)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _mvcBuilder = mvcBuilder ?? throw new ArgumentNullException(nameof(mvcBuilder));
            
            _intermediateProvider = services.BuildServiceProvider();
            var loggerFactory = _intermediateProvider.GetRequiredService<ILoggerFactory>();
            
            _resourceGraphBuilder = new ResourceGraphBuilder(_options, loggerFactory);
            _serviceDiscoveryFacade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, loggerFactory);
        }
        
        /// <summary>
        /// Executes the action provided by the user to configure <see cref="JsonApiOptions"/>.
        /// </summary>
        public void ConfigureJsonApiOptions(Action<JsonApiOptions> configureOptions)
        {
            configureOptions?.Invoke(_options);
        }
        
        /// <summary>
        /// Executes the action provided by the user to configure <see cref="ServiceDiscoveryFacade"/>.
        /// </summary>
        public void ConfigureAutoDiscovery(Action<ServiceDiscoveryFacade> configureAutoDiscovery)
        {
            configureAutoDiscovery?.Invoke(_serviceDiscoveryFacade);
        }

        /// <summary>
        /// Configures and builds the resource graph with resources from the provided sources and adds it to the DI container. 
        /// </summary>
        public void AddResourceGraph(ICollection<Type> dbContextTypes, Action<ResourceGraphBuilder> configureResourceGraph)
        {
            _serviceDiscoveryFacade.DiscoverResources();

            foreach (var dbContextType in dbContextTypes)
            {
                var dbContext = (DbContext)_intermediateProvider.GetRequiredService(dbContextType);
                AddResourcesFromDbContext(dbContext, _resourceGraphBuilder);
            }

            configureResourceGraph?.Invoke(_resourceGraphBuilder);

            _resourceGraph = _resourceGraphBuilder.Build();
            _services.AddSingleton(_resourceGraph);
        }

        /// <summary>
        /// Configures built-in ASP.NET Core MVC components. Most of this configuration can be adjusted for the developers' need.
        /// </summary>
        public void ConfigureMvc()
        {
            _mvcBuilder.AddMvcOptions(options =>
            {
                options.EnableEndpointRouting = true;
                options.Filters.AddService<IAsyncJsonApiExceptionFilter>();
                options.Filters.AddService<IAsyncQueryStringActionFilter>();
                options.Filters.AddService<IAsyncConvertEmptyActionResultFilter>();
                ConfigureMvcOptions?.Invoke(options);
            });

            if (_options.AutoGenerateControllers)
            {
                RegisterControllerFeatureProvider();
            }

            if (_options.ValidateModelState)
            {
                _mvcBuilder.AddDataAnnotations();
                _services.AddSingleton<IModelMetadataProvider, JsonApiModelMetadataProvider>();
            }
        }

        private void RegisterControllerFeatureProvider()
        {
            IJsonApiControllerGenerator controllerFeatureProvider;
            if (_services.Any(descriptor => descriptor.ServiceType == typeof(IJsonApiControllerGenerator)))
            {
                using (var provider = _services.BuildServiceProvider())
                {
                    controllerFeatureProvider = provider.GetRequiredService<IJsonApiControllerGenerator>();
                }
            }
            else
            {
                controllerFeatureProvider = new JsonApiControllerGenerator(_resourceGraph);
            }

            _mvcBuilder.ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(controllerFeatureProvider));
        }

        /// <summary>
        /// Discovers DI registrable services in the assemblies marked for discovery.
        /// </summary>
        public void DiscoverInjectables()
        {
            _serviceDiscoveryFacade.DiscoverInjectables();
        }

        /// <summary>
        /// Registers the remaining internals.
        /// </summary>
        public void ConfigureServiceContainer(ICollection<Type> dbContextTypes)
        {
            if (dbContextTypes.Any())
            {
                _services.AddScoped(typeof(DbContextResolver<>));

                foreach (var dbContextType in dbContextTypes)
                {
                    var contextResolverType = typeof(DbContextResolver<>).MakeGenericType(dbContextType);
                    _services.AddScoped(typeof(IDbContextResolver), contextResolverType);
                }
            }

            AddResourceLayer();
            AddRepositoryLayer();
            AddServiceLayer();
            AddMiddlewareLayer();
            AddSerializationLayer();
            AddQueryStringLayer();

            AddResourceHooks();

            _services.AddScoped<IGenericServiceFactory, GenericServiceFactory>();
            _services.AddScoped(typeof(IResourceChangeTracker<>), typeof(ResourceChangeTracker<>));
            _services.AddScoped<IPaginationContext, PaginationContext>();
            _services.AddScoped<IQueryLayerComposer, QueryLayerComposer>();
            _services.AddScoped<IInverseNavigationResolver, InverseNavigationResolver>();
        }

        private void AddMiddlewareLayer()
        {
            _services.AddSingleton<IJsonApiOptions>(_options);
            _services.AddSingleton<IJsonApiApplicationBuilder>(this);
            _services.AddSingleton<IExceptionHandler, ExceptionHandler>();
            _services.AddScoped<IAsyncJsonApiExceptionFilter, AsyncJsonApiExceptionFilter>();
            _services.AddScoped<IAsyncQueryStringActionFilter, AsyncQueryStringActionFilter>();
            _services.AddScoped<IAsyncConvertEmptyActionResultFilter, AsyncConvertEmptyActionResultFilter>();
            _services.AddSingleton<IJsonApiInputFormatter, JsonApiInputFormatter>();
            _services.AddSingleton<IJsonApiOutputFormatter, JsonApiOutputFormatter>();
            _services.AddSingleton<IJsonApiRoutingConvention, JsonApiRoutingConvention>();
            _services.AddSingleton<IControllerResourceMapping>(sp => sp.GetRequiredService<IJsonApiRoutingConvention>());
            _services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            _services.AddSingleton<IRequestScopedServiceProvider, RequestScopedServiceProvider>();
            _services.AddScoped<IJsonApiRequest, JsonApiRequest>();
            _services.AddScoped<IJsonApiWriter, JsonApiWriter>();
            _services.AddScoped<IJsonApiReader, JsonApiReader>();
            _services.AddScoped<ITargetedFields, TargetedFields>();
            _services.AddScoped<IFieldsToSerialize, FieldsToSerialize>();
        }

        private void AddResourceLayer()
        {
            RegisterImplementationForOpenInterfaces(ServiceDiscoveryFacade.ResourceDefinitionInterfaces, 
                typeof(JsonApiResourceDefinition<>), typeof(JsonApiResourceDefinition<,>));

            _services.AddScoped<IResourceDefinitionAccessor, ResourceDefinitionAccessor>();
            _services.AddScoped<IResourceFactory, ResourceFactory>();
            _services.AddSingleton<IResourceContextProvider>(sp => sp.GetRequiredService<IResourceGraph>());
        }

        private void AddRepositoryLayer()
        {
            RegisterImplementationForOpenInterfaces(ServiceDiscoveryFacade.RepositoryInterfaces, 
                typeof(EntityFrameworkCoreRepository<>), typeof(EntityFrameworkCoreRepository<,>));

            _services.AddScoped<IResourceRepositoryAccessor, ResourceRepositoryAccessor>();
        }

        private void AddServiceLayer()
        {
            RegisterImplementationForOpenInterfaces(ServiceDiscoveryFacade.ServiceInterfaces, 
                typeof(JsonApiResourceService<>), typeof(JsonApiResourceService<,>));
        }

        private void RegisterImplementationForOpenInterfaces(HashSet<Type> openGenericInterfaces, Type intImplementation, Type implementation)
        {
            foreach (var openGenericInterface in openGenericInterfaces)
            {
                var implementationType = openGenericInterface.GetGenericArguments().Length == 1
                    ? intImplementation
                    : implementation;

                _services.AddScoped(openGenericInterface, implementationType);
            }
        }

        private void AddQueryStringLayer()
        {
            _services.AddScoped<IIncludeQueryStringParameterReader, IncludeQueryStringParameterReader>();
            _services.AddScoped<IFilterQueryStringParameterReader, FilterQueryStringParameterReader>();
            _services.AddScoped<ISortQueryStringParameterReader, SortQueryStringParameterReader>();
            _services.AddScoped<ISparseFieldSetQueryStringParameterReader, SparseFieldSetQueryStringParameterReader>();
            _services.AddScoped<IPaginationQueryStringParameterReader, PaginationQueryStringParameterReader>();
            _services.AddScoped<IDefaultsQueryStringParameterReader, DefaultsQueryStringParameterReader>();
            _services.AddScoped<INullsQueryStringParameterReader, NullsQueryStringParameterReader>();
            _services.AddScoped<IResourceDefinitionQueryableParameterReader, ResourceDefinitionQueryableParameterReader>();

            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<IIncludeQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<IFilterQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<ISortQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<ISparseFieldSetQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<IPaginationQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<IDefaultsQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<INullsQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetRequiredService<IResourceDefinitionQueryableParameterReader>());

            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<IIncludeQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<IFilterQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<ISortQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<ISparseFieldSetQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<IPaginationQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetRequiredService<IResourceDefinitionQueryableParameterReader>());

            _services.AddScoped<IQueryStringReader, QueryStringReader>();
            _services.AddSingleton<IRequestQueryStringAccessor, RequestQueryStringAccessor>();
        }

        private void AddResourceHooks()
        { 
            if (_options.EnableResourceHooks)
            {
                _services.AddSingleton(typeof(IHooksDiscovery<>), typeof(HooksDiscovery<>));
                _services.AddScoped(typeof(IResourceHookContainer<>), typeof(ResourceHooksDefinition<>));
                _services.AddTransient<IResourceHookExecutor, ResourceHookExecutor>();
                _services.AddTransient<IHookExecutorHelper, HookExecutorHelper>();
                _services.AddScoped<ITraversalHelper, TraversalHelper>();
                _services.AddScoped<IResourceHookExecutorFacade, ResourceHookExecutorFacade>();
            }
            else
            {
                _services.AddSingleton<IResourceHookExecutorFacade, NeverResourceHookExecutorFacade>();
            }
        }

        private void AddSerializationLayer()
        {
            _services.AddScoped<IIncludedResourceObjectBuilder, IncludedResourceObjectBuilder>();
            _services.AddScoped<IJsonApiDeserializer, RequestDeserializer>();
            _services.AddScoped<IResourceObjectBuilderSettingsProvider, ResourceObjectBuilderSettingsProvider>();
            _services.AddScoped<IJsonApiSerializerFactory, ResponseSerializerFactory>();
            _services.AddScoped<ILinkBuilder, LinkBuilder>();
            _services.AddScoped<IResponseMeta, EmptyResponseMeta>();
            _services.AddScoped<IMetaBuilder, MetaBuilder>();
            _services.AddScoped(typeof(ResponseSerializer<>));
            _services.AddScoped(sp => sp.GetRequiredService<IJsonApiSerializerFactory>().GetSerializer());
            _services.AddScoped<IResourceObjectBuilder, ResponseResourceObjectBuilder>();
        }

        private void AddResourcesFromDbContext(DbContext dbContext, ResourceGraphBuilder builder)
        {
            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                builder.Add(entityType.ClrType);
            }
        }
        
        public void Dispose()
        {
            _intermediateProvider.Dispose();
        }
    }
}
