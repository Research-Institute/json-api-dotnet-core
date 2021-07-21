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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.SparseFieldSets
{
    public sealed class SparseFieldSetTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public SparseFieldSetTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlogPostsController>();
            testContext.UseController<WebAccountsController>();
            testContext.UseController<BlogsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddSingleton<ResourceCaptureStore>();

                services.AddResourceRepository<ResultCapturingRepository<Blog>>();
                services.AddResourceRepository<ResultCapturingRepository<BlogPost>>();
                services.AddResourceRepository<ResultCapturingRepository<WebAccount>>();
            });
        }

        [Fact]
        public async Task Can_select_fields_in_primary_resources()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?fields[blogPosts]=caption,author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(post.Caption);
            responseDocument.ManyData[0].Relationships.Should().HaveCount(1);
            responseDocument.ManyData[0].Relationships["author"].Data.Should().BeNull();
            responseDocument.ManyData[0].Relationships["author"].Links.Self.Should().NotBeNull();
            responseDocument.ManyData[0].Relationships["author"].Links.Related.Should().NotBeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Caption.Should().Be(post.Caption);
            postCaptured.Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_attribute_in_primary_resources()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?fields[blogPosts]=caption";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(post.Caption);
            responseDocument.ManyData[0].Relationships.Should().BeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Caption.Should().Be(post.Caption);
            postCaptured.Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_relationship_in_primary_resources()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?fields[blogPosts]=author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes.Should().BeNull();
            responseDocument.ManyData[0].Relationships.Should().HaveCount(1);
            responseDocument.ManyData[0].Relationships["author"].Data.Should().BeNull();
            responseDocument.ManyData[0].Relationships["author"].Links.Self.Should().NotBeNull();
            responseDocument.ManyData[0].Relationships["author"].Links.Related.Should().NotBeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Caption.Should().BeNull();
            postCaptured.Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_in_secondary_resources()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts?fields[blogPosts]=caption,labels";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[0].StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(blog.Posts[0].Caption);
            responseDocument.ManyData[0].Relationships.Should().HaveCount(1);
            responseDocument.ManyData[0].Relationships["labels"].Data.Should().BeNull();
            responseDocument.ManyData[0].Relationships["labels"].Links.Self.Should().NotBeNull();
            responseDocument.ManyData[0].Relationships["labels"].Links.Related.Should().NotBeNull();

            var blogCaptured = (Blog)store.Resources.Should().ContainSingle(resource => resource is Blog).And.Subject.Single();
            blogCaptured.Id.Should().Be(blog.Id);
            blogCaptured.Title.Should().BeNull();

            blogCaptured.Posts.Should().HaveCount(1);
            blogCaptured.Posts[0].Caption.Should().Be(blog.Posts[0].Caption);
            blogCaptured.Posts[0].Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_in_primary_resource_by_ID()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?fields[blogPosts]=url,author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["url"].Should().Be(post.Url);
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["author"].Data.Should().BeNull();
            responseDocument.SingleData.Relationships["author"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["author"].Links.Related.Should().NotBeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Url.Should().Be(post.Url);
            postCaptured.Caption.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_of_HasOne_relationship()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();
            post.Author = _fakers.WebAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?include=author&fields[webAccounts]=displayName,emailAddress,preferences";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);
            responseDocument.SingleData.Relationships["author"].SingleData.Id.Should().Be(post.Author.StringId);
            responseDocument.SingleData.Relationships["author"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["author"].Links.Related.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Attributes.Should().HaveCount(2);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(post.Author.DisplayName);
            responseDocument.Included[0].Attributes["emailAddress"].Should().Be(post.Author.EmailAddress);
            responseDocument.Included[0].Relationships.Should().HaveCount(1);
            responseDocument.Included[0].Relationships["preferences"].Data.Should().BeNull();
            responseDocument.Included[0].Relationships["preferences"].Links.Self.Should().NotBeNull();
            responseDocument.Included[0].Relationships["preferences"].Links.Related.Should().NotBeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Id.Should().Be(post.Id);
            postCaptured.Caption.Should().Be(post.Caption);

            postCaptured.Author.DisplayName.Should().Be(post.Author.DisplayName);
            postCaptured.Author.EmailAddress.Should().Be(post.Author.EmailAddress);
            postCaptured.Author.UserName.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_of_HasMany_relationship()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            WebAccount account = _fakers.WebAccount.Generate();
            account.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Accounts.Add(account);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/webAccounts/{account.StringId}?include=posts&fields[blogPosts]=caption,labels";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(account.StringId);
            responseDocument.SingleData.Attributes["displayName"].Should().Be(account.DisplayName);
            responseDocument.SingleData.Relationships["posts"].ManyData.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["posts"].ManyData[0].Id.Should().Be(account.Posts[0].StringId);
            responseDocument.SingleData.Relationships["posts"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["posts"].Links.Related.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
            responseDocument.Included[0].Attributes["caption"].Should().Be(account.Posts[0].Caption);
            responseDocument.Included[0].Relationships.Should().HaveCount(1);
            responseDocument.Included[0].Relationships["labels"].Data.Should().BeNull();
            responseDocument.Included[0].Relationships["labels"].Links.Self.Should().NotBeNull();
            responseDocument.Included[0].Relationships["labels"].Links.Related.Should().NotBeNull();

            var accountCaptured = (WebAccount)store.Resources.Should().ContainSingle(resource => resource is WebAccount).And.Subject.Single();
            accountCaptured.Id.Should().Be(account.Id);
            accountCaptured.DisplayName.Should().Be(account.DisplayName);

            accountCaptured.Posts.Should().HaveCount(1);
            accountCaptured.Posts[0].Caption.Should().Be(account.Posts[0].Caption);
            accountCaptured.Posts[0].Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_of_HasMany_relationship_on_secondary_resource()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/owner?include=posts&fields[blogPosts]=caption,comments";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.Owner.StringId);
            responseDocument.SingleData.Attributes["displayName"].Should().Be(blog.Owner.DisplayName);
            responseDocument.SingleData.Relationships["posts"].ManyData.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["posts"].ManyData[0].Id.Should().Be(blog.Owner.Posts[0].StringId);
            responseDocument.SingleData.Relationships["posts"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["posts"].Links.Related.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
            responseDocument.Included[0].Attributes["caption"].Should().Be(blog.Owner.Posts[0].Caption);
            responseDocument.Included[0].Relationships.Should().HaveCount(1);
            responseDocument.Included[0].Relationships["comments"].Data.Should().BeNull();
            responseDocument.Included[0].Relationships["comments"].Links.Self.Should().NotBeNull();
            responseDocument.Included[0].Relationships["comments"].Links.Related.Should().NotBeNull();

            var blogCaptured = (Blog)store.Resources.Should().ContainSingle(resource => resource is Blog).And.Subject.Single();
            blogCaptured.Id.Should().Be(blog.Id);
            blogCaptured.Owner.Should().NotBeNull();
            blogCaptured.Owner.DisplayName.Should().Be(blog.Owner.DisplayName);

            blogCaptured.Owner.Posts.Should().HaveCount(1);
            blogCaptured.Owner.Posts[0].Caption.Should().Be(blog.Owner.Posts[0].Caption);
            blogCaptured.Owner.Posts[0].Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_fields_of_HasManyThrough_relationship()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

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

            string route = $"/blogPosts/{post.StringId}?include=labels&fields[labels]=color";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);
            responseDocument.SingleData.Relationships["labels"].ManyData.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["labels"].ManyData[0].Id.Should().Be(post.BlogPostLabels.ElementAt(0).Label.StringId);
            responseDocument.SingleData.Relationships["labels"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["labels"].Links.Related.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
            responseDocument.Included[0].Attributes["color"].Should().Be(post.BlogPostLabels.Single().Label.Color.ToString("G"));
            responseDocument.Included[0].Relationships.Should().BeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Id.Should().Be(post.Id);
            postCaptured.Caption.Should().Be(post.Caption);

            postCaptured.BlogPostLabels.Should().HaveCount(1);
            postCaptured.BlogPostLabels.Single().Label.Color.Should().Be(post.BlogPostLabels.Single().Label.Color);
            postCaptured.BlogPostLabels.Single().Label.Name.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_attributes_in_multiple_resource_types()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}?include=owner.posts&fields[blogs]=title&fields[webAccounts]=userName,displayName&fields[blogPosts]=caption";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["title"].Should().Be(blog.Title);
            responseDocument.SingleData.Relationships.Should().BeNull();

            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Id.Should().Be(blog.Owner.StringId);
            responseDocument.Included[0].Attributes.Should().HaveCount(2);
            responseDocument.Included[0].Attributes["userName"].Should().Be(blog.Owner.UserName);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(blog.Owner.DisplayName);
            responseDocument.Included[0].Relationships.Should().BeNull();

            responseDocument.Included[1].Id.Should().Be(blog.Owner.Posts[0].StringId);
            responseDocument.Included[1].Attributes.Should().HaveCount(1);
            responseDocument.Included[1].Attributes["caption"].Should().Be(blog.Owner.Posts[0].Caption);
            responseDocument.Included[1].Relationships.Should().BeNull();

            var blogCaptured = (Blog)store.Resources.Should().ContainSingle(resource => resource is Blog).And.Subject.Single();
            blogCaptured.Id.Should().Be(blog.Id);
            blogCaptured.Title.Should().Be(blog.Title);
            blogCaptured.PlatformName.Should().BeNull();

            blogCaptured.Owner.UserName.Should().Be(blog.Owner.UserName);
            blogCaptured.Owner.DisplayName.Should().Be(blog.Owner.DisplayName);
            blogCaptured.Owner.DateOfBirth.Should().BeNull();

            blogCaptured.Owner.Posts.Should().HaveCount(1);
            blogCaptured.Owner.Posts[0].Caption.Should().Be(blog.Owner.Posts[0].Caption);
            blogCaptured.Owner.Posts[0].Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_only_top_level_fields_with_multiple_includes()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}?include=owner.posts&fields[blogs]=title,owner";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["title"].Should().Be(blog.Title);
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["owner"].SingleData.Id.Should().Be(blog.Owner.StringId);
            responseDocument.SingleData.Relationships["owner"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["owner"].Links.Related.Should().NotBeNull();

            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Id.Should().Be(blog.Owner.StringId);
            responseDocument.Included[0].Attributes["userName"].Should().Be(blog.Owner.UserName);
            responseDocument.Included[0].Attributes["displayName"].Should().Be(blog.Owner.DisplayName);
            responseDocument.Included[0].Attributes["dateOfBirth"].Should().BeCloseTo(blog.Owner.DateOfBirth);
            responseDocument.Included[0].Relationships["posts"].ManyData.Should().HaveCount(1);
            responseDocument.Included[0].Relationships["posts"].ManyData[0].Id.Should().Be(blog.Owner.Posts[0].StringId);
            responseDocument.Included[0].Relationships["posts"].Links.Self.Should().NotBeNull();
            responseDocument.Included[0].Relationships["posts"].Links.Related.Should().NotBeNull();

            responseDocument.Included[1].Id.Should().Be(blog.Owner.Posts[0].StringId);
            responseDocument.Included[1].Attributes["caption"].Should().Be(blog.Owner.Posts[0].Caption);
            responseDocument.Included[1].Attributes["url"].Should().Be(blog.Owner.Posts[0].Url);
            responseDocument.Included[1].Relationships["labels"].Data.Should().BeNull();
            responseDocument.Included[1].Relationships["labels"].Links.Self.Should().NotBeNull();
            responseDocument.Included[1].Relationships["labels"].Links.Related.Should().NotBeNull();

            var blogCaptured = (Blog)store.Resources.Should().ContainSingle(resource => resource is Blog).And.Subject.Single();
            blogCaptured.Id.Should().Be(blog.Id);
            blogCaptured.Title.Should().Be(blog.Title);
            blogCaptured.PlatformName.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_ID()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?fields[blogPosts]=id,caption";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
            responseDocument.ManyData[0].Attributes["caption"].Should().Be(post.Caption);
            responseDocument.ManyData[0].Relationships.Should().BeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Id.Should().Be(post.Id);
            postCaptured.Caption.Should().Be(post.Caption);
            postCaptured.Url.Should().BeNull();
        }

        [Fact]
        public async Task Can_select_empty_fieldset()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?fields[blogPosts]=";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.StringId);
            responseDocument.ManyData[0].Attributes.Should().BeNull();
            responseDocument.ManyData[0].Relationships.Should().BeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Id.Should().Be(post.Id);
            postCaptured.Url.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_select_on_unknown_resource_type()
        {
            // Arrange
            const string route = "/webAccounts?fields[doesNotExist]=id";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified fieldset is invalid.");
            error.Detail.Should().Be("Resource type 'doesNotExist' does not exist.");
            error.Source.Parameter.Should().Be("fields[doesNotExist]");
        }

        [Fact]
        public async Task Cannot_select_attribute_with_blocked_capability()
        {
            // Arrange
            WebAccount account = _fakers.WebAccount.Generate();

            string route = $"/webAccounts/{account.Id}?fields[webAccounts]=password";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Retrieving the requested attribute is not allowed.");
            error.Detail.Should().Be("Retrieving the attribute 'password' is not allowed.");
            error.Source.Parameter.Should().Be("fields[webAccounts]");
        }

        [Fact]
        public async Task Retrieves_all_properties_when_fieldset_contains_readonly_attribute()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            Blog blog = _fakers.Blog.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}?fields[blogs]=showAdvertisements";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(blog.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["showAdvertisements"].Should().Be(blog.ShowAdvertisements);
            responseDocument.SingleData.Relationships.Should().BeNull();

            var blogCaptured = (Blog)store.Resources.Should().ContainSingle(resource => resource is Blog).And.Subject.Single();
            blogCaptured.ShowAdvertisements.Should().Be(blogCaptured.ShowAdvertisements);
            blogCaptured.Title.Should().Be(blog.Title);
        }

        [Fact]
        public async Task Can_select_fields_on_resource_type_multiple_times()
        {
            // Arrange
            var store = _testContext.Factory.Services.GetRequiredService<ResourceCaptureStore>();
            store.Clear();

            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?fields[blogPosts]=url&fields[blogPosts]=caption,url&fields[blogPosts]=caption,author";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(post.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(2);
            responseDocument.SingleData.Attributes["caption"].Should().Be(post.Caption);
            responseDocument.SingleData.Attributes["url"].Should().Be(post.Url);
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["author"].Data.Should().BeNull();
            responseDocument.SingleData.Relationships["author"].Links.Self.Should().NotBeNull();
            responseDocument.SingleData.Relationships["author"].Links.Related.Should().NotBeNull();

            var postCaptured = (BlogPost)store.Resources.Should().ContainSingle(resource => resource is BlogPost).And.Subject.Single();
            postCaptured.Id.Should().Be(post.Id);
            postCaptured.Caption.Should().Be(post.Caption);
            postCaptured.Url.Should().Be(postCaptured.Url);
        }
    }
}
