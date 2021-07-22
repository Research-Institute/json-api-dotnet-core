using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class InheritanceTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext> _testContext;

        public InheritanceTests(ExampleIntegrationTestContext<TestableStartup<InheritanceDbContext>, InheritanceDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<MenController>();
        }

        [Fact]
        public async Task Can_create_resource_with_inherited_attributes()
        {
            // Arrange
            var newMan = new Man
            {
                FamilyName = "Smith",
                IsRetired = true,
                HasBeard = true
            };

            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    attributes = new
                    {
                        familyName = newMan.FamilyName,
                        isRetired = newMan.IsRetired,
                        hasBeard = newMan.HasBeard
                    }
                }
            };

            const string route = "/men";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("men");
            responseDocument.SingleData.Attributes["familyName"].Should().Be(newMan.FamilyName);
            responseDocument.SingleData.Attributes["isRetired"].Should().Be(newMan.IsRetired);
            responseDocument.SingleData.Attributes["hasBeard"].Should().Be(newMan.HasBeard);

            int newManId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.FirstWithIdAsync(newManId);

                manInDatabase.FamilyName.Should().Be(newMan.FamilyName);
                manInDatabase.IsRetired.Should().Be(newMan.IsRetired);
                manInDatabase.HasBeard.Should().Be(newMan.HasBeard);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_ToOne_relationship()
        {
            // Arrange
            var existingInsurance = new CompanyHealthInsurance();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<CompanyHealthInsurance>();
                dbContext.CompanyHealthInsurances.Add(existingInsurance);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new
                    {
                        healthInsurance = new
                        {
                            data = new
                            {
                                type = "companyHealthInsurances",
                                id = existingInsurance.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/men";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            int newManId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.Include(man => man.HealthInsurance).FirstWithIdAsync(newManId);

                manInDatabase.HealthInsurance.Should().BeOfType<CompanyHealthInsurance>();
                manInDatabase.HealthInsurance.Id.Should().Be(existingInsurance.Id);
            });
        }

        [Fact]
        public async Task Can_update_resource_through_primary_endpoint()
        {
            // Arrange
            var existingMan = new Man
            {
                FamilyName = "Smith",
                IsRetired = false,
                HasBeard = true
            };

            var newMan = new Man
            {
                FamilyName = "Jackson",
                IsRetired = true,
                HasBeard = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Men.Add(existingMan);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    id = existingMan.StringId,
                    attributes = new
                    {
                        familyName = newMan.FamilyName,
                        isRetired = newMan.IsRetired,
                        hasBeard = newMan.HasBeard
                    }
                }
            };

            string route = "/men/" + existingMan.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.FirstWithIdAsync(existingMan.Id);

                manInDatabase.FamilyName.Should().Be(newMan.FamilyName);
                manInDatabase.IsRetired.Should().Be(newMan.IsRetired);
                manInDatabase.HasBeard.Should().Be(newMan.HasBeard);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_ToOne_relationship_through_relationship_endpoint()
        {
            // Arrange
            var existingMan = new Man();
            var existingInsurance = new CompanyHealthInsurance();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<Man, CompanyHealthInsurance>();
                dbContext.AddInRange(existingMan, existingInsurance);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "companyHealthInsurances",
                    id = existingInsurance.StringId
                }
            };

            string route = $"/men/{existingMan.StringId}/relationships/healthInsurance";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.Include(man => man.HealthInsurance).FirstWithIdAsync(existingMan.Id);

                manInDatabase.HealthInsurance.Should().BeOfType<CompanyHealthInsurance>();
                manInDatabase.HealthInsurance.Id.Should().Be(existingInsurance.Id);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_ToMany_relationship()
        {
            // Arrange
            var existingFather = new Man();
            var existingMother = new Woman();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<Woman, Man>();
                dbContext.Humans.AddRange(existingFather, existingMother);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new
                    {
                        parents = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "men",
                                    id = existingFather.StringId
                                },
                                new
                                {
                                    type = "women",
                                    id = existingMother.StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/men";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            int newManId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.Include(man => man.Parents).FirstWithIdAsync(newManId);

                manInDatabase.Parents.Should().HaveCount(2);
                manInDatabase.Parents.Should().ContainSingle(human => human is Man);
                manInDatabase.Parents.Should().ContainSingle(human => human is Woman);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_ToMany_relationship_through_relationship_endpoint()
        {
            // Arrange
            var existingChild = new Man();
            var existingFather = new Man();
            var existingMother = new Woman();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<Woman, Man>();
                dbContext.Humans.AddRange(existingChild, existingFather, existingMother);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "men",
                        id = existingFather.StringId
                    },
                    new
                    {
                        type = "women",
                        id = existingMother.StringId
                    }
                }
            };

            string route = $"/men/{existingChild.StringId}/relationships/parents";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Man manInDatabase = await dbContext.Men.Include(man => man.Parents).FirstWithIdAsync(existingChild.Id);

                manInDatabase.Parents.Should().HaveCount(2);
                manInDatabase.Parents.Should().ContainSingle(human => human is Man);
                manInDatabase.Parents.Should().ContainSingle(human => human is Woman);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_ManyToMany_relationship()
        {
            // Arrange
            var existingBook = new Book();
            var existingVideo = new Video();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<Book, Video, Man>();
                dbContext.ContentItems.AddRange(existingBook, existingVideo);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "men",
                    relationships = new
                    {
                        favoriteContent = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "books",
                                    id = existingBook.StringId
                                },
                                new
                                {
                                    type = "videos",
                                    id = existingVideo.StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/men";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            int newManId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                List<ContentItem> contentItems = await dbContext.HumanFavoriteContentItems
                    .Where(favorite => favorite.Human.Id == newManId)
                    .Select(favorite => favorite.ContentItem)
                    .ToListAsync();

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                contentItems.Should().HaveCount(2);
                contentItems.Should().ContainSingle(item => item is Book);
                contentItems.Should().ContainSingle(item => item is Video);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_ManyToMany_relationship_through_relationship_endpoint()
        {
            // Arrange
            var existingBook = new Book();
            var existingVideo = new Video();
            var existingMan = new Man();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<HumanFavoriteContentItem, Book, Video, Man>();
                dbContext.AddInRange(existingBook, existingVideo, existingMan);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "books",
                        id = existingBook.StringId
                    },
                    new
                    {
                        type = "videos",
                        id = existingVideo.StringId
                    }
                }
            };

            string route = $"/men/{existingMan.StringId}/relationships/favoriteContent";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                List<ContentItem> contentItems = await dbContext.HumanFavoriteContentItems
                    .Where(favorite => favorite.Human.Id == existingMan.Id)
                    .Select(favorite => favorite.ContentItem)
                    .ToListAsync();

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                contentItems.Should().HaveCount(2);
                contentItems.Should().ContainSingle(item => item is Book);
                contentItems.Should().ContainSingle(item => item is Video);
            });
        }
    }
}
