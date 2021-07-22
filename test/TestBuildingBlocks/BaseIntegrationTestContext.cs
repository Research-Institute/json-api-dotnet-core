using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TestBuildingBlocks
{
    /// <summary>
    /// Base class for a test context that creates a new database and server instance before running tests and cleans up afterwards. You can either use this
    /// as a fixture on your tests class (init/cleanup runs once before/after all tests) or have your tests class inherit from it (init/cleanup runs once
    /// before/after each test). See <see href="https://xunit.net/docs/shared-context" /> for details on shared context usage.
    /// </summary>
    /// <typeparam name="TStartup">
    /// The server Startup class, which can be defined in the test project.
    /// </typeparam>
    /// <typeparam name="TRemoteStartup">
    /// The base class for <typeparamref name="TStartup" />, which MUST be defined in the API project.
    /// </typeparam>
    /// <typeparam name="TDbContext">
    /// The EF Core database context, which can be defined in the test project.
    /// </typeparam>
    public abstract class BaseIntegrationTestContext<TStartup, TRemoteStartup, TDbContext> : IntegrationTest, IDisposable
        where TStartup : class
        where TRemoteStartup : class
        where TDbContext : DbContext
    {
        private readonly Lazy<WebApplicationFactory<TRemoteStartup>> _lazyFactory;
        private readonly TestControllerProvider _testControllerProvider = new();
        private Action<ILoggingBuilder> _loggingConfiguration;
        private Action<IServiceCollection> _beforeServicesConfiguration;
        private Action<IServiceCollection> _afterServicesConfiguration;

        public WebApplicationFactory<TRemoteStartup> Factory => _lazyFactory.Value;

        protected BaseIntegrationTestContext()
        {
            _lazyFactory = new Lazy<WebApplicationFactory<TRemoteStartup>>(CreateFactory);
        }

        public void UseController<TController>()
            where TController : ControllerBase
        {
            _testControllerProvider.AddController(typeof(TController));
        }

        protected override HttpClient CreateClient()
        {
            return Factory.CreateClient();
        }

        private WebApplicationFactory<TRemoteStartup> CreateFactory()
        {
            string postgresPassword = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "postgres";
            string dbConnectionString = $"Host=localhost;Port=5432;Database=JsonApiTest-{Guid.NewGuid():N};User ID=postgres;Password={postgresPassword}";

            var factory = new IntegrationTestWebApplicationFactory();

            factory.ConfigureLogging(_loggingConfiguration);

            factory.ConfigureServicesBeforeStartup(services =>
            {
                _beforeServicesConfiguration?.Invoke(services);

                services.ReplaceControllers(_testControllerProvider);

                services.AddDbContext<TDbContext>(options =>
                {
                    options.UseNpgsql(dbConnectionString);
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
            });

            factory.ConfigureServicesAfterStartup(_afterServicesConfiguration);

            using IServiceScope scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            dbContext.Database.EnsureCreated();

            return factory;
        }

        public void Dispose()
        {
            RunOnDatabaseAsync(async context => await context.Database.EnsureDeletedAsync()).Wait();

            Factory.Dispose();
        }

        public void ConfigureLogging(Action<ILoggingBuilder> loggingConfiguration)
        {
            _loggingConfiguration = loggingConfiguration;
        }

        public void ConfigureServicesBeforeStartup(Action<IServiceCollection> servicesConfiguration)
        {
            _beforeServicesConfiguration = servicesConfiguration;
        }

        public void ConfigureServicesAfterStartup(Action<IServiceCollection> servicesConfiguration)
        {
            _afterServicesConfiguration = servicesConfiguration;
        }

        public async Task RunOnDatabaseAsync(Func<TDbContext, Task> asyncAction)
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

            await asyncAction(dbContext);
        }

        private sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<TRemoteStartup>
        {
            private Action<ILoggingBuilder> _loggingConfiguration;
            private Action<IServiceCollection> _beforeServicesConfiguration;
            private Action<IServiceCollection> _afterServicesConfiguration;

            public void ConfigureLogging(Action<ILoggingBuilder> loggingConfiguration)
            {
                _loggingConfiguration = loggingConfiguration;
            }

            public void ConfigureServicesBeforeStartup(Action<IServiceCollection> servicesConfiguration)
            {
                _beforeServicesConfiguration = servicesConfiguration;
            }

            public void ConfigureServicesAfterStartup(Action<IServiceCollection> servicesConfiguration)
            {
                _afterServicesConfiguration = servicesConfiguration;
            }

            protected override IHostBuilder CreateHostBuilder()
            {
                return Host.CreateDefaultBuilder(null).ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureLogging(options =>
                    {
                        _loggingConfiguration?.Invoke(options);
                    });

                    webBuilder.ConfigureServices(services =>
                    {
                        _beforeServicesConfiguration?.Invoke(services);
                    });

                    webBuilder.UseStartup<TStartup>();

                    webBuilder.ConfigureServices(services =>
                    {
                        _afterServicesConfiguration?.Invoke(services);
                    });
                });
            }
        }
    }
}
