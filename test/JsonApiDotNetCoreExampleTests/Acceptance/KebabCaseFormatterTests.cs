using System;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    public sealed class KebabCaseFormatterTests : IClassFixture<IntegrationTestContext<KebabCaseStartup, AppDbContext>>
    {
        private readonly IntegrationTestContext<KebabCaseStartup, AppDbContext> _testContext;
        private readonly Faker<KebabCasedModel> _faker;

        public KebabCaseFormatterTests(IntegrationTestContext<KebabCaseStartup, AppDbContext> testContext)
        {
            _testContext = testContext;

            _faker = new Faker<KebabCasedModel>()
                .RuleFor(m => m.CompoundAttr, f => f.Lorem.Sentence());
        }

        [Fact]
        public async Task KebabCaseFormatter_GetAll_IsReturned()
        {
            // Arrange
            var model = _faker.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<KebabCasedModel>();
                dbContext.KebabCasedModels.Add(model);

                await dbContext.SaveChangesAsync();
            });

            var route = "api/v1/kebab-cased-models";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(model.StringId);
            responseDocument.ManyData[0].Attributes["compound-attr"].Should().Be(model.CompoundAttr);
        }

        [Fact]
        public async Task KebabCaseFormatter_GetSingle_IsReturned()
        {
            // Arrange
            var model = _faker.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.KebabCasedModels.Add(model);

                await dbContext.SaveChangesAsync();
            });

            var route = "api/v1/kebab-cased-models/" + model.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(model.StringId);
            responseDocument.SingleData.Attributes["compound-attr"].Should().Be(model.CompoundAttr);
        }

        [Fact]
        public async Task KebabCaseFormatter_Create_IsCreated()
        {
            // Arrange
            var model = _faker.Generate();
            var serializer = GetSerializer<KebabCasedModel>(kcm => new { kcm.CompoundAttr });

            var route = "api/v1/kebab-cased-models";

            var requestBody = serializer.Serialize(model);

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["compound-attr"].Should().Be(model.CompoundAttr);
        }

        [Fact]
        public async Task KebabCaseFormatter_Update_IsUpdated()
        {
            // Arrange
            var model = _faker.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.KebabCasedModels.Add(model);

                await dbContext.SaveChangesAsync();
            });

            model.CompoundAttr = _faker.Generate().CompoundAttr;
            var serializer = GetSerializer<KebabCasedModel>(kcm => new { kcm.CompoundAttr });

            var route = "api/v1/kebab-cased-models/" + model.StringId;

            var requestBody = serializer.Serialize(model);

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var stored = await dbContext.KebabCasedModels.SingleAsync(x => x.Id == model.Id);
                Assert.Equal(model.CompoundAttr, stored.CompoundAttr);
            });
        }

        [Fact]
        public async Task KebabCaseFormatter_ErrorWithStackTrace_CasingConventionIsApplied()
        {
            // Arrange
            var route = "api/v1/kebab-cased-models/1";
            
            const string requestBody = "{ \"data\": {";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<JObject>(route, requestBody);

            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            var meta = responseDocument["errors"][0]["meta"];
            Assert.NotNull(meta["stack-trace"]);
        }

        private IRequestSerializer GetSerializer<TResource>(Expression<Func<TResource, dynamic>> attributes = null, Expression<Func<TResource, dynamic>> relationships = null) where TResource : class, IIdentifiable
        {
            using var scope = _testContext.Factory.Services.CreateScope();
            var serializer = scope.ServiceProvider.GetRequiredService<IRequestSerializer>();
            var graph = scope.ServiceProvider.GetRequiredService<IResourceGraph>();
            
            serializer.AttributesToSerialize = attributes != null ? graph.GetAttributes(attributes) : null;
            serializer.RelationshipsToSerialize = relationships != null ? graph.GetRelationships(relationships) : null;
            
            return serializer;
        }
    }

    public sealed class KebabCaseStartup : TestStartup
    {
        public KebabCaseStartup(IConfiguration configuration) : base(configuration)
        {
        }

        protected override void ConfigureJsonApiOptions(JsonApiOptions options)
        {
            base.ConfigureJsonApiOptions(options);

            ((DefaultContractResolver)options.SerializerSettings.ContractResolver).NamingStrategy = new KebabCaseNamingStrategy();
        }
    }
    
    public sealed class KebabCasedModelsController : JsonApiController<KebabCasedModel>
    {
        public KebabCasedModelsController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<KebabCasedModel> resourceService)
            : base(options, loggerFactory, resourceService)
        { }
    }
}
