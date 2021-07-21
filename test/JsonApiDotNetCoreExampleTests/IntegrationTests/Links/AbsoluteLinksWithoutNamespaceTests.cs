using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Links
{
    public sealed class AbsoluteLinksWithoutNamespaceTests
        : IClassFixture<ExampleIntegrationTestContext<AbsoluteLinksNoNamespaceStartup<LinksDbContext>, LinksDbContext>>
    {
        private const string HostPrefix = "http://localhost";

        private readonly ExampleIntegrationTestContext<AbsoluteLinksNoNamespaceStartup<LinksDbContext>, LinksDbContext> _testContext;
        private readonly LinksFakers _fakers = new();

        public AbsoluteLinksWithoutNamespaceTests(ExampleIntegrationTestContext<AbsoluteLinksNoNamespaceStartup<LinksDbContext>, LinksDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<PhotoAlbumsController>();
            testContext.UseController<PhotosController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddScoped(typeof(IResourceChangeTracker<>), typeof(NeverSameResourceChangeTracker<>));
            });

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.IncludeTotalResourceCount = true;
        }

        [Fact]
        public async Task Get_primary_resource_by_ID_returns_absolute_links()
        {
            // Arrange
            PhotoAlbum album = _fakers.PhotoAlbum.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync();
            });

            string route = "/photoAlbums/" + album.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.SingleData.Relationships["photos"].Links.Self.Should().Be(HostPrefix + route + "/relationships/photos");
            responseDocument.SingleData.Relationships["photos"].Links.Related.Should().Be(HostPrefix + route + "/photos");
        }

        [Fact]
        public async Task Get_primary_resources_with_include_returns_absolute_links()
        {
            // Arrange
            PhotoAlbum album = _fakers.PhotoAlbum.Generate();
            album.Photos = _fakers.Photo.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<PhotoAlbum>();
                dbContext.PhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/photoAlbums?include=photos";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().Be(HostPrefix + route);
            responseDocument.Links.Last.Should().Be(HostPrefix + route);
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            string albumLink = HostPrefix + $"/photoAlbums/{album.StringId}";

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Links.Self.Should().Be(albumLink);
            responseDocument.ManyData[0].Relationships["photos"].Links.Self.Should().Be(albumLink + "/relationships/photos");
            responseDocument.ManyData[0].Relationships["photos"].Links.Related.Should().Be(albumLink + "/photos");

            string photoLink = HostPrefix + $"/photos/{album.Photos.ElementAt(0).StringId}";

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Links.Self.Should().Be(photoLink);
            responseDocument.Included[0].Relationships["album"].Links.Self.Should().Be(photoLink + "/relationships/album");
            responseDocument.Included[0].Relationships["album"].Links.Related.Should().Be(photoLink + "/album");
        }

        [Fact]
        public async Task Get_secondary_resource_returns_absolute_links()
        {
            // Arrange
            Photo photo = _fakers.Photo.Generate();
            photo.Album = _fakers.PhotoAlbum.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Photos.Add(photo);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/photos/{photo.StringId}/album";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            string albumLink = HostPrefix + $"/photoAlbums/{photo.Album.StringId}";

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Links.Self.Should().Be(albumLink);
            responseDocument.SingleData.Relationships["photos"].Links.Self.Should().Be(albumLink + "/relationships/photos");
            responseDocument.SingleData.Relationships["photos"].Links.Related.Should().Be(albumLink + "/photos");
        }

        [Fact]
        public async Task Get_secondary_resources_returns_absolute_links()
        {
            // Arrange
            PhotoAlbum album = _fakers.PhotoAlbum.Generate();
            album.Photos = _fakers.Photo.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/photoAlbums/{album.StringId}/photos";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().Be(HostPrefix + route);
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            string photoLink = HostPrefix + $"/photos/{album.Photos.ElementAt(0).StringId}";

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Links.Self.Should().Be(photoLink);
            responseDocument.ManyData[0].Relationships["album"].Links.Self.Should().Be(photoLink + "/relationships/album");
            responseDocument.ManyData[0].Relationships["album"].Links.Related.Should().Be(photoLink + "/album");
        }

        [Fact]
        public async Task Get_HasOne_relationship_returns_absolute_links()
        {
            // Arrange
            Photo photo = _fakers.Photo.Generate();
            photo.Album = _fakers.PhotoAlbum.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Photos.Add(photo);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/photos/{photo.StringId}/relationships/album";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().Be(HostPrefix + $"/photos/{photo.StringId}/album");
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Links.Should().BeNull();
            responseDocument.SingleData.Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Get_HasMany_relationship_returns_absolute_links()
        {
            // Arrange
            PhotoAlbum album = _fakers.PhotoAlbum.Generate();
            album.Photos = _fakers.Photo.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/photoAlbums/{album.StringId}/relationships/photos";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().Be(HostPrefix + $"/photoAlbums/{album.StringId}/photos");
            responseDocument.Links.First.Should().Be(HostPrefix + route);
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Links.Should().BeNull();
            responseDocument.ManyData[0].Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Create_resource_with_side_effects_and_include_returns_absolute_links()
        {
            // Arrange
            Photo existingPhoto = _fakers.Photo.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Photos.Add(existingPhoto);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "photoAlbums",
                    relationships = new
                    {
                        photos = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "photos",
                                    id = existingPhoto.StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/photoAlbums?include=photos";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            string albumLink = HostPrefix + $"/photoAlbums/{responseDocument.SingleData.Id}";

            responseDocument.SingleData.Links.Self.Should().Be(albumLink);
            responseDocument.SingleData.Relationships["photos"].Links.Self.Should().Be(albumLink + "/relationships/photos");
            responseDocument.SingleData.Relationships["photos"].Links.Related.Should().Be(albumLink + "/photos");

            string photoLink = HostPrefix + $"/photos/{existingPhoto.StringId}";

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Links.Self.Should().Be(photoLink);
            responseDocument.Included[0].Relationships["album"].Links.Self.Should().Be(photoLink + "/relationships/album");
            responseDocument.Included[0].Relationships["album"].Links.Related.Should().Be(photoLink + "/album");
        }

        [Fact]
        public async Task Update_resource_with_side_effects_and_include_returns_absolute_links()
        {
            // Arrange
            Photo existingPhoto = _fakers.Photo.Generate();
            PhotoAlbum existingAlbum = _fakers.PhotoAlbum.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingPhoto, existingAlbum);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "photos",
                    id = existingPhoto.StringId,
                    relationships = new
                    {
                        album = new
                        {
                            data = new
                            {
                                type = "photoAlbums",
                                id = existingAlbum.StringId
                            }
                        }
                    }
                }
            };

            string route = $"/photos/{existingPhoto.StringId}?include=album";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Related.Should().BeNull();
            responseDocument.Links.First.Should().BeNull();
            responseDocument.Links.Last.Should().BeNull();
            responseDocument.Links.Prev.Should().BeNull();
            responseDocument.Links.Next.Should().BeNull();

            string photoLink = HostPrefix + $"/photos/{existingPhoto.StringId}";

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Links.Self.Should().Be(photoLink);
            responseDocument.SingleData.Relationships["album"].Links.Self.Should().Be(photoLink + "/relationships/album");
            responseDocument.SingleData.Relationships["album"].Links.Related.Should().Be(photoLink + "/album");

            string albumLink = HostPrefix + $"/photoAlbums/{existingAlbum.StringId}";

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Links.Self.Should().Be(albumLink);
            responseDocument.Included[0].Relationships["photos"].Links.Self.Should().Be(albumLink + "/relationships/photos");
            responseDocument.Included[0].Relationships["photos"].Links.Related.Should().Be(albumLink + "/photos");
        }
    }
}
