using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.Filtering
{
    public sealed class FilterDepthTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public FilterDepthTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlogsController>();
            testContext.UseController<BlogPostsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.EnableLegacyFilterNotation = false;

            options.DisableTopPagination = false;
            options.DisableChildrenPagination = false;
        }

        [Fact]
        public async Task Can_filter_in_primary_resources()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);
            posts[0].Caption = "One";
            posts[1].Caption = "Two";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?filter=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(posts[1].StringId);
        }

        [Fact]
        public async Task Cannot_filter_in_single_primary_resource()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?filter=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified filter is invalid.");
            error.Detail.Should().Be("This query string parameter can only be used on a collection of resources (not on a single resource).");
            error.Source.Parameter.Should().Be("filter");
        }

        [Fact]
        public async Task Can_filter_in_secondary_resources()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(2);
            blog.Posts[0].Caption = "One";
            blog.Posts[1].Caption = "Two";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts?filter=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[1].StringId);
        }

        [Fact]
        public async Task Cannot_filter_in_single_secondary_resource()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}/author?filter=equals(displayName,'John Smith')";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified filter is invalid.");
            error.Detail.Should().Be("This query string parameter can only be used on a collection of resources (not on a single resource).");
            error.Source.Parameter.Should().Be("filter");
        }

        [Fact]
        public async Task Can_filter_on_HasOne_relationship()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(3);
            posts[0].Author = _fakers.WebAccount.Generate();
            posts[0].Author.UserName = "Conner";
            posts[1].Author = _fakers.WebAccount.Generate();
            posts[1].Author.UserName = "Smith";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?include=author&filter=or(equals(author.userName,'Smith'),equals(author,null))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData.Should().ContainSingle(post => post.Id == posts[1].StringId);
            responseDocument.ManyData.Should().ContainSingle(post => post.Id == posts[2].StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(posts[1].Author.StringId);
        }

        [Fact]
        public async Task Can_filter_on_HasMany_relationship()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(2);
            blogs[1].Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?filter=greaterThan(count(posts),'0')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blogs[1].StringId);
        }

        [Fact]
        public async Task Can_filter_on_HasMany_relationship_with_nested_condition()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(2);
            blogs[0].Posts = _fakers.BlogPost.Generate(1);
            blogs[1].Posts = _fakers.BlogPost.Generate(1);
            blogs[1].Posts[0].Comments = _fakers.Comment.Generate(1).ToHashSet();
            blogs[1].Posts[0].Comments.ElementAt(0).Text = "ABC";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?filter=has(posts,has(comments,startsWith(text,'A')))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blogs[1].StringId);
        }

        [Fact]
        public async Task Can_filter_on_HasManyThrough_relationship()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);

            posts[1].BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?filter=has(labels)";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(posts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_on_HasManyThrough_relationship_with_nested_condition()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(2);
            blogs[0].Posts = _fakers.BlogPost.Generate(1);
            blogs[1].Posts = _fakers.BlogPost.Generate(1);

            blogs[1].Posts[0].BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = new Label
                    {
                        Color = LabelColor.Green
                    }
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?filter=has(posts,has(labels,equals(color,'Green')))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blogs[1].StringId);
        }

        [Fact]
        public async Task Can_filter_in_scope_of_HasMany_relationship()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(2);
            blog.Posts[0].Caption = "One";
            blog.Posts[1].Caption = "Two";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?include=posts&filter[posts]=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(blog.Posts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_in_scope_of_HasMany_relationship_on_secondary_resource()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(2);
            blog.Owner.Posts[0].Caption = "One";
            blog.Owner.Posts[1].Caption = "Two";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/owner?include=posts&filter[posts]=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(blog.Owner.Posts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_in_scope_of_HasManyThrough_relationship()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);

            posts[0].BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            posts[1].BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            posts[0].BlogPostLabels.Single().Label.Name = "Cold";
            posts[1].BlogPostLabels.Single().Label.Name = "Hot";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            // Workaround for https://github.com/dotnet/efcore/issues/21026
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DisableTopPagination = false;
            options.DisableChildrenPagination = true;

            const string route = "/blogPosts?include=labels&filter[labels]=equals(name,'Hot')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(posts[1].BlogPostLabels.First().Label.StringId);
        }

        [Fact]
        public async Task Can_filter_in_scope_of_relationship_chain()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(2);
            blog.Owner.Posts[0].Caption = "One";
            blog.Owner.Posts[1].Caption = "Two";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?include=owner.posts&filter[owner.posts]=equals(caption,'Two')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);

            responseDocument.Included.Should().HaveCount(2);
            responseDocument.Included[0].Id.Should().Be(blog.Owner.StringId);
            responseDocument.Included[1].Id.Should().Be(blog.Owner.Posts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_in_same_scope_multiple_times()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(3);
            posts[0].Caption = "One";
            posts[1].Caption = "Two";
            posts[2].Caption = "Three";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?filter=equals(caption,'One')&filter=equals(caption,'Three')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(posts[0].StringId);
            responseDocument.ManyData[1].Id.Should().Be(posts[2].StringId);
        }

        [Fact]
        public async Task Can_filter_in_same_scope_multiple_times_using_legacy_notation()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.EnableLegacyFilterNotation = true;

            List<BlogPost> posts = _fakers.BlogPost.Generate(3);
            posts[0].Author = _fakers.WebAccount.Generate();
            posts[1].Author = _fakers.WebAccount.Generate();
            posts[2].Author = _fakers.WebAccount.Generate();

            posts[0].Author.UserName = "Joe";
            posts[0].Author.DisplayName = "Smith";

            posts[1].Author.UserName = "John";
            posts[1].Author.DisplayName = "Doe";

            posts[2].Author.UserName = "Jack";
            posts[2].Author.DisplayName = "Miller";

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?filter[author.userName]=John&filter[author.displayName]=Smith";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(posts[0].StringId);
            responseDocument.ManyData[1].Id.Should().Be(posts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_in_multiple_scopes()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(2);
            blogs[1].Title = "Technology";
            blogs[1].Owner = _fakers.WebAccount.Generate();
            blogs[1].Owner.UserName = "Smith";
            blogs[1].Owner.Posts = _fakers.BlogPost.Generate(2);
            blogs[1].Owner.Posts[0].Caption = "One";
            blogs[1].Owner.Posts[1].Caption = "Two";
            blogs[1].Owner.Posts[1].Comments = _fakers.Comment.Generate(2).ToHashSet();
            blogs[1].Owner.Posts[1].Comments.ElementAt(0).CreatedAt = 1.January(2000);
            blogs[1].Owner.Posts[1].Comments.ElementAt(1).CreatedAt = 10.January(2010);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            // @formatter:keep_existing_linebreaks true

            const string route = "/blogs?include=owner.posts.comments&" +
                "filter=and(equals(title,'Technology'),has(owner.posts),equals(owner.userName,'Smith'))&" +
                "filter[owner.posts]=equals(caption,'Two')&" +
                "filter[owner.posts.comments]=greaterThan(createdAt,'2005-05-05')";

            // @formatter:keep_existing_linebreaks restore

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blogs[1].StringId);

            responseDocument.Included.Should().HaveCount(3);
            responseDocument.Included[0].Id.Should().Be(blogs[1].Owner.StringId);
            responseDocument.Included[1].Id.Should().Be(blogs[1].Owner.Posts[1].StringId);
            responseDocument.Included[2].Id.Should().Be(blogs[1].Owner.Posts[1].Comments.Skip(1).First().StringId);
        }
    }
}
