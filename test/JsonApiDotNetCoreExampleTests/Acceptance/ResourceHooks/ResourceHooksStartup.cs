using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Definitions;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCoreExampleTests.IntegrationTests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    public sealed class ResourceHooksStartup : TestableStartup<AppDbContext>
    {
        public ResourceHooksStartup(IConfiguration configuration) : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddClientSerialization();

            services.AddSingleton<ISystemClock, SystemClock>();

            services.AddTransient<ResourceHooksDefinition<Article>, ArticleHooks>();
            services.AddTransient<ResourceHooksDefinition<Passport>, PassportHooks>();
            services.AddTransient<ResourceHooksDefinition<Tag>, TagHooks>();
            services.AddTransient<ResourceHooksDefinition<Person>, PersonHooks>();
            services.AddTransient<ResourceHooksDefinition<TodoItem>, TodoItemHooks>();

            base.ConfigureServices(services);
        }

        protected override void SetJsonApiOptions(JsonApiOptions options)
        {
            base.SetJsonApiOptions(options);
        
            options.EnableResourceHooks = true;
            options.LoadDatabaseValues = true;
        }
    }
}
