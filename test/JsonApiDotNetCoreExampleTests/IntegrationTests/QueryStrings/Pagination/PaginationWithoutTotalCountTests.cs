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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.Pagination
{
    public sealed class PaginationWithoutTotalCountTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private const string HostPrefix = "http://localhost";
        private const int DefaultPageSize = 5;

        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public PaginationWithoutTotalCountTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlogPostsController>();
            testContext.UseController<WebAccountsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.IncludeTotalResourceCount = false;
            options.DefaultPageSize = new PageSize(DefaultPageSize);
            options.AllowUnknownQueryStringParameters = true;
        }

        [Fact]
        public async Task Hides_pagination_links_when_unconstrained_page_size()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DefaultPageSize = null;

            const string route = "/blogPosts?foo=bar";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Renders_pagination_links_when_page_size_is_specified_in_query_string_with_no_data()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DefaultPageSize = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?page[size]=8&foo=bar";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + route);
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Renders_pagination_links_when_page_number_is_specified_in_query_string_with_no_data()
        {
            // Arrange
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?page[number]=2&foo=bar";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogPosts?foo=bar");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(HostPrefix + "/blogPosts?foo=bar");
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Renders_pagination_links_when_page_number_is_specified_in_query_string_with_partially_filled_page()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(12);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?foo=bar&page[number]=3";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Count.Should().BeLessThan(DefaultPageSize);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogPosts?foo=bar");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(HostPrefix + "/blogPosts?foo=bar&page[number]=2");
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Renders_pagination_links_when_page_number_is_specified_in_query_string_with_full_page()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(DefaultPageSize * 3);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?page[number]=3&foo=bar";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(DefaultPageSize);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogPosts?foo=bar");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(HostPrefix + "/blogPosts?page[number]=2&foo=bar");
            responseDocument.Links.Next.Should().Be(HostPrefix + "/blogPosts?page[number]=4&foo=bar");
        }

        [Fact]
        public async Task Renders_pagination_links_when_page_number_is_specified_in_query_string_with_full_page_on_secondary_endpoint()
        {
            // Arrange
            WebAccount account = _fakers.WebAccount.Generate();
            account.Posts = _fakers.BlogPost.Generate(DefaultPageSize * 3);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Accounts.Add(account);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/webAccounts/{account.StringId}/posts?page[number]=3&foo=bar";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(DefaultPageSize);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + $"/webAccounts/{account.StringId}/posts?foo=bar");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(HostPrefix + $"/webAccounts/{account.StringId}/posts?page[number]=2&foo=bar");
            responseDocument.Links.Next.Should().Be(HostPrefix + $"/webAccounts/{account.StringId}/posts?page[number]=4&foo=bar");
        }
    }
}
