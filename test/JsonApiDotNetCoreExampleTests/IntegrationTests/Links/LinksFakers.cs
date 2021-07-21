using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Links
{
    internal sealed class LinksFakers : FakerContainer
    {
        private readonly Lazy<Faker<PhotoAlbum>> _lazyPhotoAlbumFaker = new(() =>
            new Faker<PhotoAlbum>()
                .UseSeed(GetFakerSeed())
                .RuleFor(photoAlbum => photoAlbum.Name, faker => faker.Lorem.Sentence()));

        private readonly Lazy<Faker<Photo>> _lazyPhotoFaker = new(() =>
            new Faker<Photo>()
                .UseSeed(GetFakerSeed())
                .RuleFor(photo => photo.Url, faker => faker.Image.PlaceImgUrl()));

        private readonly Lazy<Faker<PhotoLocation>> _lazyPhotoLocationFaker = new(() =>
            new Faker<PhotoLocation>()
                .UseSeed(GetFakerSeed())
                .RuleFor(photoLocation => photoLocation.PlaceName, faker => faker.Address.FullAddress())
                .RuleFor(photoLocation => photoLocation.Latitude, faker => faker.Address.Latitude())
                .RuleFor(photoLocation => photoLocation.Longitude, faker => faker.Address.Longitude()));

        public Faker<PhotoAlbum> PhotoAlbum => _lazyPhotoAlbumFaker.Value;
        public Faker<Photo> Photo => _lazyPhotoFaker.Value;
        public Faker<PhotoLocation> PhotoLocation => _lazyPhotoLocationFaker.Value;
    }
}
