using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    public sealed class IdObfuscationStartup : TestableStartup<ObfuscationDbContext>
    {
        public IdObfuscationStartup(IConfiguration configuration) : base(configuration)
        {
        }

        protected override void SetJsonApiOptions(JsonApiOptions options)
        {
            base.SetJsonApiOptions(options);

            options.ValidateModelState = true;
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IJsonApiControllerGenerator, ObfuscatedJsonApiControllerGenerator>();
            base.ConfigureServices(services);
        }
    }
}
