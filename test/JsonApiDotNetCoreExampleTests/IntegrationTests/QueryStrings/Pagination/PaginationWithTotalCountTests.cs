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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings.Pagination
{
    public sealed class PaginationWithTotalCountTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private const string HostPrefix = "http://localhost";
        private const int DefaultPageSize = 5;

        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public PaginationWithTotalCountTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlogPostsController>();
            testContext.UseController<BlogsController>();
            testContext.UseController<WebAccountsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.IncludeTotalResourceCount = true;
            options.DefaultPageSize = new PageSize(DefaultPageSize);
            options.MaximumPageSize = null;
            options.MaximumPageNumber = null;
            options.AllowUnknownQueryStringParameters = true;

            options.DisableTopPagination = false;
            options.DisableChildrenPagination = false;
        }

        [Fact]
        public async Task Can_paginate_in_primary_resources()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogPosts?page[number]=2&page[size]=1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogPosts?page[size]=1");
            responseDocument.Links.Last.Should().Be(responseDocument.Links.Self);
            responseDocument.Links.Prev.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_paginate_in_single_primary_resource()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}?page[number]=2";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified paging is invalid.");
            error.Detail.Should().Be("This query string parameter can only be used on a collection of resources (not on a single resource).");
            error.Source.Parameter.Should().Be("page[number]");
        }

        [Fact]
        public async Task Can_paginate_in_secondary_resources()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts?page[number]=2&page[size]=1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + $"/blogs/{blog.StringId}/posts?page[size]=1");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Next.Should().Be(HostPrefix + $"/blogs/{blog.StringId}/posts?page[number]=3&page[size]=1");
        }

        [Fact]
        public async Task Cannot_paginate_in_single_secondary_resource()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}/author?page[size]=5";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified paging is invalid.");
            error.Detail.Should().Be("This query string parameter can only be used on a collection of resources (not on a single resource).");
            error.Source.Parameter.Should().Be("page[size]");
        }

        [Fact]
        public async Task Can_paginate_in_scope_of_HasMany_relationship()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(3);
            blogs[0].Posts = _fakers.BlogPost.Generate(2);
            blogs[1].Posts = _fakers.BlogPost.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?include=posts&page[number]=posts:2&page[size]=2,posts:1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Id.Should().Be(blogs[0].Posts[1].StringId);
            responseDocument.Included[1].Id.Should().Be(blogs[1].Posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogs?include=posts&page[size]=2,posts:1");
            responseDocument.Links.Last.Should().Be(HostPrefix + "/blogs?include=posts&page[number]=2&page[size]=2,posts:1");
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().Be(responseDocument.Links.Last);
        }

        [Fact]
        public async Task Can_paginate_in_scope_of_HasMany_relationship_on_secondary_resource()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Owner = _fakers.WebAccount.Generate();
            blog.Owner.Posts = _fakers.BlogPost.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/owner?include=posts&page[number]=posts:2&page[size]=posts:1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(blog.Owner.Posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Can_paginate_HasMany_relationship_on_relationship_endpoint()
        {
            // Arrange
            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/relationships/posts?page[number]=2&page[size]=1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + $"/blogs/{blog.StringId}/relationships/posts?page[size]=1");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Can_paginate_in_scope_of_HasManyThrough_relationship()
        {
            // Arrange
            List<BlogPost> posts = _fakers.BlogPost.Generate(2);

            posts[0].BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                },
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
                },
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

            // Workaround for https://github.com/dotnet/efcore/issues/21026
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DisableTopPagination = true;
            options.DisableChildrenPagination = false;

            const string route = "/blogPosts?include=labels&page[number]=labels:2&page[size]=labels:1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Id.Should().Be(posts[0].BlogPostLabels.Skip(1).First().Label.StringId);
            responseDocument.Included[1].Id.Should().Be(posts[1].BlogPostLabels.Skip(1).First().Label.StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + "/blogPosts?include=labels&page[size]=labels:1");
            responseDocument.Links.Last.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Can_paginate_HasManyThrough_relationship_on_relationship_endpoint()
        {
            // Arrange
            BlogPost post = _fakers.BlogPost.Generate();

            post.BlogPostLabels = new HashSet<BlogPostLabel>
            {
                new()
                {
                    Label = _fakers.Label.Generate()
                },
                new()
                {
                    Label = _fakers.Label.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.Add(post);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogPosts/{post.StringId}/relationships/labels?page[number]=2&page[size]=1";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(post.BlogPostLabels.ElementAt(1).Label.StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(HostPrefix + $"/blogPosts/{post.StringId}/relationships/labels?page[size]=1");
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Can_paginate_in_multiple_scopes()
        {
            // Arrange
            List<Blog> blogs = _fakers.Blog.Generate(2);
            blogs[1].Owner = _fakers.WebAccount.Generate();
            blogs[1].Owner.Posts = _fakers.BlogPost.Generate(2);
            blogs[1].Owner.Posts[1].Comments = _fakers.Comment.Generate(2).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Blog>();
                dbContext.Blogs.AddRange(blogs);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/blogs?include=owner.posts.comments&page[size]=1,owner.posts:1,owner.posts.comments:1&" +
                "page[number]=2,owner.posts:2,owner.posts.comments:2";

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

            const string linkPrefix = HostPrefix + "/blogs?include=owner.posts.comments";

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(linkPrefix + "&page[size]=1,owner.posts:1,owner.posts.comments:1");
            responseDocument.Links.Last.Should().Be(linkPrefix + "&page[size]=1,owner.posts:1,owner.posts.comments:1&page[number]=2");
            responseDocument.Links.Prev.Should().Be(responseDocument.Links.First);
            responseDocument.Links.Next.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_paginate_in_unknown_scope()
        {
            // Arrange
            const string route = "/webAccounts?page[number]=doesNotExist:1";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified paging is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' does not exist on resource 'webAccounts'.");
            error.Source.Parameter.Should().Be("page[number]");
        }

        [Fact]
        public async Task Cannot_paginate_in_unknown_nested_scope()
        {
            // Arrange
            const string route = "/webAccounts?page[size]=posts.doesNotExist:1";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("The specified paging is invalid.");
            error.Detail.Should().Be("Relationship 'doesNotExist' in 'posts.doesNotExist' does not exist on resource 'blogPosts'.");
            error.Source.Parameter.Should().Be("page[size]");
        }

        [Fact]
        public async Task Uses_default_page_number_and_size()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DefaultPageSize = new PageSize(2);

            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(3);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(blog.Posts[0].StringId);
            responseDocument.ManyData[1].Id.Should().Be(blog.Posts[1].StringId);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().Be(responseDocument.Links.Self);
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().Be(HostPrefix + $"/blogs/{blog.StringId}/posts?page[number]=2");
        }

        [Fact]
        public async Task Returns_all_resources_when_paging_is_disabled()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DefaultPageSize = null;

            Blog blog = _fakers.Blog.Generate();
            blog.Posts = _fakers.BlogPost.Generate(25);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/blogs/{blog.StringId}/posts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(25);

            responseDocument.Links.Should().NotBeNull();
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();
        }

        [Theory]
        [InlineData(1, 1, 4, null, 2)]
        [InlineData(2, 1, 4, 1, 3)]
        [InlineData(3, 1, 4, 2, 4)]
        [InlineData(4, 1, 4, 3, null)]
        public async Task Renders_correct_top_level_links_for_page_number(int pageNumber, int? firstLink, int? lastLink, int? prevLink, int? nextLink)
        {
            // Arrange
            WebAccount account = _fakers.WebAccount.Generate();
            account.UserName = "&" + account.UserName;

            const int totalCount = 3 * DefaultPageSize + 3;
            List<BlogPost> posts = _fakers.BlogPost.Generate(totalCount);

            foreach (BlogPost post in posts)
            {
                post.Author = account;
            }

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BlogPost>();
                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();
            });

            string routePrefix = "/blogPosts?filter=equals(author.userName,'" + WebUtility.UrlEncode(account.UserName) + "')" +
                "&fields[webAccounts]=userName&include=author&sort=id&foo=bar,baz";

            string route = routePrefix + $"&page[number]={pageNumber}";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);

            if (firstLink != null)
            {
                string expected = HostPrefix + SetPageNumberInUrl(routePrefix, firstLink.Value);
                responseDocument.Links.First.Should().Be(expected);
            }
            else
            {
                responseDocument.Links.First.Should().BeNull();
            }

            if (prevLink != null)
            {
                string expected = HostPrefix + SetPageNumberInUrl(routePrefix, prevLink.Value);
                responseDocument.Links.Prev.Should().Be(expected);
            }
            else
            {
                responseDocument.Links.Prev.Should().BeNull();
            }

            if (nextLink != null)
            {
                string expected = HostPrefix + SetPageNumberInUrl(routePrefix, nextLink.Value);
                responseDocument.Links.Next.Should().Be(expected);
            }
            else
            {
                responseDocument.Links.Next.Should().BeNull();
            }

            if (lastLink != null)
            {
                string expected = HostPrefix + SetPageNumberInUrl(routePrefix, lastLink.Value);
                responseDocument.Links.Last.Should().Be(expected);
            }
            else
            {
                responseDocument.Links.Last.Should().BeNull();
            }

            static string SetPageNumberInUrl(string url, int pageNumber)
            {
                return pageNumber != 1 ? url + "&page[number]=" + pageNumber : url;
            }
        }
    }
}
