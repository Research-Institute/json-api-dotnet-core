using System.Collections.Generic;
using System.Linq;
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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.Includes
{
    public sealed class IncludeTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public IncludeTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlogPostsController>();
            testContext.UseController<BlogsController>();
            testContext.UseController<CommentsController>();
            testContext.UseController<WebAccountsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.MaximumIncludeDepth = null;
        }

        [Fact]
        public async Task Can_include_in_primary_resources()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();
            post.Author = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "blogPosts?include=author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(post.Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(post.Author.StringId);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(post.Author.DisplayName);
        }

        [Fact]
        public async Task Can_include_in_primary_resource_by_ID()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();
            post.Author = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"blogPosts/{post.StringId}?include=author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(post.Author.StringId);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(post.Author.DisplayName);
        }

        [Fact]
        public async Task Can_include_in_secondary_resource()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/owner?include=posts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.Owner.StringId);
            responseDocument.SingleData.Attributes["displayName"].Should().Be(blog.Owner.DisplayName);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(blog.Owner.Posts[0].StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(blog.Owner.Posts[0].Caption);
        }

        [Fact]
        public async Task Can_include_in_secondary_resources()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(1);
            blog.Posts[0].Author = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts?include=author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[0].StringId);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(blog.Posts[0].Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(blog.Posts[0].Author.StringId);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(blog.Posts[0].Author.DisplayName);
        }

        [Fact]
        public async Task Can_include_HasOne_relationships()
        {
            // Arrange
            Comment comment = _fakers.Comment.Generate();
            comment.Author = _fakers.WebAccount.Generate();
            comment.Parent = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/comments/{comment.StringId}?include=author,parent";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(comment.StringId);
            responseDocument.SingleData.Attributes["text"].Should().Be(comment.Text);

            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(comment.Author.StringId);
            responseDocument.Included[0].Attributes["userName"].Should().Be(comment.Author.UserName);

            responseDocument.Included[1].Type.Should().Be("blogPosts");
            responseDocument.Included[1].Id.Should().Be(comment.Parent.StringId);
            responseDocument.Included[1].Attributes["caption"].Should().Be(comment.Parent.Caption);
        }

        [Fact]
        public async Task Can_include_HasMany_relationship()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();
            post.Comments = _fakers.Comment.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"blogPosts/{post.StringId}?include=comments";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("comments");
            responseDocument.Included[0].Id.Should().Be(post.Comments.Single().StringId);
            responseDocument.Included[0].Attributes["createdAt"].Should().BeCloseTo(post.Comments.Single().CreatedAt);
        }

        [Fact]
        public async Task Can_include_HasManyThrough_relationship()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            post.BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"blogPosts/{post.StringId}?include=labels";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("labels");
            responseDocument.Included[0].Id.Should().Be(post.BlogPostLabels.Single().Label.StringId);
            responseDocument.Included[0].Attributes["name"].Should().Be(post.BlogPostLabels.Single().Label.Name);
        }

        [Fact]
        public async Task Can_include_HasManyThrough_relationship_in_secondary_resource()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            post.BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}/labels?include=posts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Type.Should().Be("labels");
            responseDocument.ManyData[0].Id.Should().Be(post.BlogPostLabels.ElementAt(0).Label.StringId);
            responseDocument.ManyData[0].Attributes["name"].Should().Be(post.BlogPostLabels.Single().Label.Name);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(post.StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(post.Caption);
        }

        [Fact]
        public async Task Can_include_chain_of_HasOne_relationships()
        {
            // Arrange
            Comment comment = _fakers.Comment.Generate();
            comment.Parent = _fakers.BlogPost.Generate();
            comment.Parent.Author = _fakers.WebAccount.Generate();
            comment.Parent.Author.Preferences = _fakers.AccountPreferences.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync();
            });

            string route = $"comments/{comment.StringId}?include=parent.author.preferences";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(comment.StringId);
            responseDocument.SingleData.Attributes["text"].Should().Be(comment.Text);

            responseDocument.Included.Should().HaveCount(3);

            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(comment.Parent.StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(comment.Parent.Caption);

            responseDocument.Included[1].Type.Should().Be("webAccounts");
            responseDocument.Included[1].Id.Should().Be(comment.Parent.Author.StringId);
            responseDocument.Included[1].Attributes["displayName"].Should().Be(comment.Parent.Author.DisplayName);

            responseDocument.Included[2].Type.Should().Be("accountPreferences");
            responseDocument.Included[2].Id.Should().Be(comment.Parent.Author.Preferences.StringId);
            responseDocument.Included[2].Attributes["useDarkTheme"].Should().Be(comment.Parent.Author.Preferences.UseDarkTheme);
        }

        [Fact]
        public async Task Can_include_chain_of_HasMany_relationships()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(1);
            blog.Posts[0].Comments = _fakers.Comment.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}?include=posts.comments";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.StringId);
            responseDocument.SingleData.Attributes["title"].Should().Be(blog.Title);

            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(blog.Posts[0].StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(blog.Posts[0].Caption);

            responseDocument.Included[1].Type.Should().Be("comments");
            responseDocument.Included[1].Id.Should().Be(blog.Posts[0].Comments.Single().StringId);
            responseDocument.Included[1].Attributes["createdAt"].Should().BeCloseTo(blog.Posts[0].Comments.Single().CreatedAt);
        }

        [Fact]
        public async Task Can_include_chain_of_recursive_relationships()
        {
            // Arrange
            Comment comment = _fakers.Comment.Generate();
            comment.Parent = _fakers.BlogPost.Generate();
            comment.Parent.Author = _fakers.WebAccount.Generate();
            comment.Parent.Comments = _fakers.Comment.Generate(2).ToHashSet();
            comment.Parent.Comments.ElementAt(0).Author = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/comments/{comment.StringId}?include=parent.comments.author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(comment.StringId);
            responseDocument.SingleData.Attributes["text"].Should().Be(comment.Text);

            responseDocument.Included.Should().HaveCount(5);

            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(comment.Parent.StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(comment.Parent.Caption);

            responseDocument.Included[1].Type.Should().Be("comments");
            responseDocument.Included[1].Id.Should().Be(comment.StringId);
            responseDocument.Included[1].Attributes["text"].Should().Be(comment.Text);

            responseDocument.Included[2].Type.Should().Be("comments");
            responseDocument.Included[2].Id.Should().Be(comment.Parent.Comments.ElementAt(0).StringId);
            responseDocument.Included[2].Attributes["text"].Should().Be(comment.Parent.Comments.ElementAt(0).Text);

            responseDocument.Included[3].Type.Should().Be("webAccounts");
            responseDocument.Included[3].Id.Should().Be(comment.Parent.Comments.ElementAt(0).Author.StringId);
            responseDocument.Included[3].Attributes["userName"].Should().Be(comment.Parent.Comments.ElementAt(0).Author.UserName);

            responseDocument.Included[4].Type.Should().Be("comments");
            responseDocument.Included[4].Id.Should().Be(comment.Parent.Comments.ElementAt(1).StringId);
            responseDocument.Included[4].Attributes["text"].Should().Be(comment.Parent.Comments.ElementAt(1).Text);
        }

        [Fact]
        public async Task Can_include_chain_of_relationships_with_multiple_paths()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(1);
            blog.Posts[0].Author = _fakers.WebAccount.Generate();
            blog.Posts[0].Author.Preferences = _fakers.AccountPreferences.Generate();
            blog.Posts[0].Comments = _fakers.Comment.Generate(2).ToHashSet();
            blog.Posts[0].Comments.ElementAt(0).Author = _fakers.WebAccount.Generate();
            blog.Posts[0].Comments.ElementAt(0).Author.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}?include=posts.author.preferences,posts.comments.author.posts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.StringId);
            responseDocument.SingleData.Attributes["title"].Should().Be(blog.Title);

            responseDocument.Included.Should().HaveCount(7);

            responseDocument.Included[0].Type.Should().Be("blogPosts");
            responseDocument.Included[0].Id.Should().Be(blog.Posts[0].StringId);
            responseDocument.Included[0].Attributes["caption"].Should().Be(blog.Posts[0].Caption);

            responseDocument.Included[1].Type.Should().Be("webAccounts");
            responseDocument.Included[1].Id.Should().Be(blog.Posts[0].Author.StringId);
            responseDocument.Included[1].Attributes["userName"].Should().Be(blog.Posts[0].Author.UserName);

            responseDocument.Included[2].Type.Should().Be("accountPreferences");
            responseDocument.Included[2].Id.Should().Be(blog.Posts[0].Author.Preferences.StringId);
            responseDocument.Included[2].Attributes["useDarkTheme"].Should().Be(blog.Posts[0].Author.Preferences.UseDarkTheme);

            responseDocument.Included[3].Type.Should().Be("comments");
            responseDocument.Included[3].Id.Should().Be(blog.Posts[0].Comments.ElementAt(0).StringId);
            responseDocument.Included[3].Attributes["text"].Should().Be(blog.Posts[0].Comments.ElementAt(0).Text);

            responseDocument.Included[4].Type.Should().Be("webAccounts");
            responseDocument.Included[4].Id.Should().Be(blog.Posts[0].Comments.ElementAt(0).Author.StringId);
            responseDocument.Included[4].Attributes["userName"].Should().Be(blog.Posts[0].Comments.ElementAt(0).Author.UserName);

            responseDocument.Included[5].Type.Should().Be("blogPosts");
            responseDocument.Included[5].Id.Should().Be(blog.Posts[0].Comments.ElementAt(0).Author.Posts[0].StringId);
            responseDocument.Included[5].Attributes["caption"].Should().Be(blog.Posts[0].Comments.ElementAt(0).Author.Posts[0].Caption);

            responseDocument.Included[6].Type.Should().Be("comments");
            responseDocument.Included[6].Id.Should().Be(blog.Posts[0].Comments.ElementAt(1).StringId);
            responseDocument.Included[6].Attributes["text"].Should().Be(blog.Posts[0].Comments.ElementAt(1).Text);
        }

        [Fact]
        public async Task Prevents_duplicate_includes_over_single_resource()
        {
            // Arrange
            WebAccount account = _fakers.WebAccount.Generate();

            BlogPost post = _fakers.BlogPost.Generate();
            post.Author = account;
            post.Reviewer = account;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?include=author&include=reviewer";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(account.StringId);
            responseDocument.Included[0].Attributes["userName"].Should().Be(account.UserName);
        }

        [Fact]
        public async Task Prevents_duplicate_includes_over_multiple_resources()
        {
            // Arrange
            WebAccount account = _fakers.WebAccount.Generate();

            List<BlogPost> posts = _fakers.BlogPost.Generate(2);
            posts[0].Author = account;
            posts[1].Author = account;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?include=author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(account.StringId);
            responseDocument.Included[0].Attributes["userName"].Should().Be(account.UserName);
        }

        [Fact]
        public async Task Cannot_include_unknown_relationship()
        {
            // Arrange
            const string route = "/webAccounts?include=doesNotExist";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified include is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' does not exist on resource 'webAccounts'.");
            error.Source.Parameter.Should().Be("include");
        }

        [Fact]
        public async Task Cannot_include_unknown_nested_relationship()
        {
            // Arrange
            const string route = "/blogs?include=posts.doesNotExist";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified include is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' in 'posts.doesNotExist' does not exist on resource 'blogPosts'.");
            error.Source.Parameter.Should().Be("include");
        }

        [Fact]
        public async Task Cannot_include_relationship_with_blocked_capability()
        {
            // Arrange
            const string route = "/blogPosts?include=parent";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Including the requested relationship is not allowed.");
            error.Detail.Should().Be("Including the relationship 'parent' on 'blogPosts' is not allowed.");
            error.Source.Parameter.Should().Be("include");
        }

        [Fact]
        public async Task Ignores_null_parent_in_nested_include()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);
            posts[0].Reviewer = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?include=reviewer.preferences";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            ResourceObject[] postWithReviewer = responseDocument.ManyData
                .Where(resource => resource.Relationships.First(pair => pair.Key == "reviewer").Value.SingleData != null).ToArray();

            postWithReviewer.Should().HaveCount(1);
            postWithReviewer[0].Attributes["caption"].Should().Be(posts[0].Caption);

            ResourceObject[] postWithoutReviewer = responseDocument.ManyData
                .Where(resource => resource.Relationships.First(pair => pair.Key == "reviewer").Value.SingleData == null).ToArray();

            postWithoutReviewer.Should().HaveCount(1);
            postWithoutReviewer[0].Attributes["caption"].Should().Be(posts[1].Caption);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("webAccounts");
            responseDocument.Included[0].Id.Should().Be(posts[0].Reviewer.StringId);
            responseDocument.Included[0].Attributes["userName"].Should().Be(posts[0].Reviewer.UserName);
        }

        [Fact]
        public async Task Can_include_at_configured_maximum_inclusion_depth()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.MaximumIncludeDepth = 1;

            Blog blog = _fakers.Blog.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts?include=author,comments";

            // Act
            (HttpResponseMessage httpResponse, _) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Cannot_exceed_configured_maximum_inclusion_depth()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.MaximumIncludeDepth = 1;

            const string route = "/blogs/123/owner?include=posts.comments";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified include is invalid.");
            error.Detail.Should().Be("Including 'posts.comments' exceeds the maximum inclusion depth of 1.");
            error.Source.Parameter.Should().Be("include");
        }
    }
}
