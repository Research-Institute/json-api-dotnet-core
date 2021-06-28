using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ExceptionHandling
{
    public sealed class ExceptionHandlerTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ErrorDbContext>, ErrorDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ErrorDbContext>, ErrorDbContext> _testContext;

        public ExceptionHandlerTests(ExampleIntegrationTestContext<TestableStartup<ErrorDbContext>, ErrorDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<ThrowingArticlesController>();
            testContext.UseController<ConsumerArticlesController>();

            FakeLoggerFactory loggerFactory = null;

            testContext.ConfigureLogging(options =>
            {
                loggerFactory = new FakeLoggerFactory();

                options.ClearProviders();
                options.AddProvider(loggerFactory);
                options.SetMinimumLevel(LogLevel.Warning);
            });

            testContext.ConfigureServicesBeforeStartup(services =>
            {
                if (loggerFactory != null)
                {
                    services.AddSingleton(_ => loggerFactory);
                }
            });

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceService<ConsumerArticleService>();
                services.AddScoped<IExceptionHandler, AlternateExceptionHandler>();
            });
        }

        [Fact(Skip = "Fails because the code timing measurements produce additional logging.")]
        public async Task Logs_and_produces_error_response_for_custom_exception()
        {
            // Arrange
            var loggerFactory = _testContext.Factory.Services.GetRequiredService<FakeLoggerFactory>();
            loggerFactory.Logger.Clear();

            var consumerArticle = new ConsumerArticle
            {
                Code = ConsumerArticleService.UnavailableArticlePrefix + "123"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.ConsumerArticles.Add(consumerArticle);
                await dbContext.SaveChangesAsync();
            });

            string route = "/consumerArticles/" + consumerArticle.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Gone);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Gone);
            error.Title.Should().Be("The requested article is no longer available.");
            error.Detail.Should().Be("Article with code 'X123' is no longer available.");
            error.Meta.Data["support"].Should().Be("Please contact us for info about similar articles at company@email.com.");

            loggerFactory.Logger.Messages.Should().HaveCount(1);
            loggerFactory.Logger.Messages.Single().LogLevel.Should().Be(LogLevel.Warning);
            loggerFactory.Logger.Messages.Single().Text.Should().Contain("Article with code 'X123' is no longer available.");
        }

        [Fact(Skip = "Fails because the code timing measurements produce additional logging.")]
        public async Task Logs_and_produces_error_response_on_serialization_failure()
        {
            // Arrange
            var loggerFactory = _testContext.Factory.Services.GetRequiredService<FakeLoggerFactory>();
            loggerFactory.Logger.Clear();

            var throwingArticle = new ThrowingArticle();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.ThrowingArticles.Add(throwingArticle);
                await dbContext.SaveChangesAsync();
            });

            string route = "/throwingArticles/" + throwingArticle.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.InternalServerError);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            error.Title.Should().Be("An unhandled error occurred while processing this request.");
            error.Detail.Should().Be("Exception has been thrown by the target of an invocation.");

            IEnumerable<string> stackTraceLines = ((JArray)error.Meta.Data["stackTrace"]).Select(token => token.Value<string>());
            stackTraceLines.Should().ContainMatch("* System.InvalidOperationException: Article status could not be determined.*");

            loggerFactory.Logger.Messages.Should().HaveCount(1);
            loggerFactory.Logger.Messages.Single().LogLevel.Should().Be(LogLevel.Error);
            loggerFactory.Logger.Messages.Single().Text.Should().Contain("Exception has been thrown by the target of an invocation.");
        }
    }
}
