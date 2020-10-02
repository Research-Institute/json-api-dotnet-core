using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class InheritanceTests : IClassFixture<IntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext>>
    {
        private readonly IntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext> _testContext;

        public InheritanceTests(IntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext> testContext)
        {
            _testContext = testContext;
        }
        
        [Fact]
        public async Task Can_create_resource_with_to_one_relationship()
        {
            // Arrange
            var cat = new Cat();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Cat>();
#pragma warning disable 4014
                dbContext.AddAsync(cat);
#pragma warning restore 4014
                await dbContext.SaveChangesAsync();
            });

            var route = "/men";
            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "pet", new
                            {
                                data = new { type = "cats", id = cat.StringId }
                            }
                        }
                    }
                }
            };

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertMan = await dbContext.Males
                    .Include(h => h.Pet)
                    .SingleAsync(h => h.Id == int.Parse(responseDocument.SingleData.Id));
                
                assertMan.Pet.GetType().Should().Be(cat.GetType());
            });
        }
        
        [Fact]
        public async Task Can_patch_resource_with_to_one_relationship_through_relationship_link()
        {
            // Arrange
            var man = new Man();
            var cat = new Cat();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Man, Cat>();

                await dbContext.AddRangeAsync(man, cat);
                await dbContext.SaveChangesAsync();
            });
            
            var route = $"/men/{man.Id}/relationships/pet";

            var requestBody = new
            {
                data = new { type = "cats", id = cat.StringId }
            };

            // Act
            var (httpResponse, _) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertMan = await dbContext.Males
                    .Include(h => h.Pet)
                    .SingleAsync(h => h.Id.Equals(man.Id));

                assertMan.Pet.GetType().Should().Be(cat.GetType());
            });
        }


        [Fact]
        public async Task Can_create_resource_with_to_many_relationship()
        {
            // Arrange
            var father = new Man();
            var mother = new Woman();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Woman, Man>();

                await dbContext.AddRangeAsync(father, mother);
                await dbContext.SaveChangesAsync();
            });

            var route = "/men";
            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "parents", new
                            {
                                data = new[]
                                {
                                    new { type = "men", id = father.StringId },
                                    new { type = "women", id = mother.StringId }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertMan = await dbContext.Males
                    .Include(h => h.Parents)
                    .SingleAsync(h => h.Id == int.Parse(responseDocument.SingleData.Id));

                assertMan.Parents.Should().HaveCount(2);
                assertMan.Parents.Should().ContainSingle(h => h is Man);
                assertMan.Parents.Should().ContainSingle(h => h is Woman);
            });
        }

        [Fact]
        public async Task Can_patch_resource_with_to_many_relationship_through_relationship_link()
        {
            // Arrange   
            var child = new Man();
            var father = new Man();
            var mother = new Woman();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Woman, Man>();

                await dbContext.AddRangeAsync(child, father, mother);
                await dbContext.SaveChangesAsync();
            });
        
            var route = $"/men/{child.StringId}/relationships/parents";
            var requestBody = new
            {
                data = new[]
                {
                    new { type = "men", id = father.StringId },
                    new { type = "women", id = mother.StringId }
                }
            };
        
            // Act
            var (httpResponse, _) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);
        
            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var assertChild = await dbContext.Males
                    .Include(h => h.Parents)
                    .SingleAsync(h => h.Id == child.Id);
                
                assertChild.Parents.Should().HaveCount(2);
                assertChild.Parents.Should().ContainSingle(h => h is Man);
                assertChild.Parents.Should().ContainSingle(h => h is Woman);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_many_to_many_relationship()
        {
            // Arrange
            var book = new Book();
            var video = new Video();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Book, Video, Man>();

                await dbContext.AddRangeAsync(book, video);
                await dbContext.SaveChangesAsync();
            });
        
            var route = "/men";
            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new Dictionary<string, object>
                    {
                        {
                            "favoriteContent", new
                            {
                                data = new[]
                                {
                                    new { type = "books", id = book.StringId },
                                    new { type = "videos", id = video.StringId }
                                }
                            }
                        }
                    }
                }
            };
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);
        
            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var favoriteContent = (await dbContext.Males
                        .Include(h => h.HumanFavoriteContentItems)
                            .ThenInclude(hp => hp.ContentItem)
                        .SingleAsync(h => h.Id == int.Parse(responseDocument.SingleData.Id)))
                    .HumanFavoriteContentItems.Select(hp => hp.ContentItem).ToList();
                
                favoriteContent.Should().HaveCount(2);
                favoriteContent.Should().ContainSingle(h => h is Book);
                favoriteContent.Should().ContainSingle(h => h is Video);
            });
        }

        [Fact]
        public async Task Can_patch_resource_with_many_to_many_relationship_through_relationship_link()
        {
            // Arrange
            var book = new Book();
            var video = new Video();
            var man = new Man();
            
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<HumanFavoriteContentItem, Book, Video, Man>();

                await dbContext.AddRangeAsync(book, video, man);
                await dbContext.SaveChangesAsync();
            });
        
            var route = $"/men/{man.Id}/relationships/favoriteContent";
            var requestBody = new
            {
                data = new[]
                {
                    new { type = "books", id = book.StringId },
                    new { type = "videos", id = video.StringId }
                }
            };
        
            // Act
            var (httpResponse, _) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);
        
            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var favoriteContent = (await dbContext.Males
                        .Include(h => h.HumanFavoriteContentItems)
                            .ThenInclude(hp => hp.ContentItem)
                        .SingleAsync(h => h.Id.Equals(man.Id)))
                    .HumanFavoriteContentItems.Select(hp => hp.ContentItem).ToList();
        
                favoriteContent.Should().HaveCount(2);
                favoriteContent.Should().ContainSingle(h => h is Book);
                favoriteContent.Should().ContainSingle(h => h is Video);
            });
        }
    }
}
