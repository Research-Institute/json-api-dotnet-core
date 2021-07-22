using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.Filtering
{
    public sealed class FilterTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public FilterTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WebAccountsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.EnableLegacyFilterNotation = false;
        }

        [Fact]
        public async Task Cannot_filter_in_unknown_scope()
        {
            // Arrange
            const string route = "/webAccounts?filter[doesNotExist]=equals(title,null)";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified filter is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' does not exist on resource 'webAccounts'.");
            error.Source.Parameter.Should().Be("filter[doesNotExist]");
        }

        [Fact]
        public async Task Cannot_filter_in_unknown_nested_scope()
        {
            // Arrange
            const string route = "/webAccounts?filter[posts.doesNotExist]=equals(title,null)";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified filter is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' in 'posts.doesNotExist' does not exist on resource 'blogPosts'.");
            error.Source.Parameter.Should().Be("filter[posts.doesNotExist]");
        }

        [Fact]
        public async Task Cannot_filter_on_attribute_with_blocked_capability()
        {
            // Arrange
            const string route = "/webAccounts?filter=equals(dateOfBirth,null)";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Filtering on the requested attribute is not allowed.");
            error.Detail.Should().Be("Filtering on attribute 'dateOfBirth' is not allowed.");
            error.Source.Parameter.Should().Be("filter");
        }

        [Fact]
        public async Task Can_filter_on_ID()
        {
            // Arrange
            List<WebAccount> accounts = _fakers.WebAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<WebAccount>();
                dbContext.Accounts.AddRange(accounts);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/webAccounts?filter=equals(id,'{accounts[0].StringId}')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(accounts[0].StringId);
            responseDocument.ManyData[0].Attributes["userName"].Should().Be(accounts[0].UserName);
        }
    }
}
