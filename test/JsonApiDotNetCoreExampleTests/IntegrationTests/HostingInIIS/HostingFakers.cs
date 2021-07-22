using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.HostingInIIS
{
    internal sealed class HostingFakers : FakerContainer
    {
        private readonly Lazy<Faker<ArtGallery>> _lazyArtGalleryFaker = new(() =>
            new Faker<ArtGallery>()
                .UseSeed(GetFakerSeed())
                .RuleFor(artGallery => artGallery.Theme, faker => faker.Lorem.Word()));

        private readonly Lazy<Faker<Painting>> _lazyPaintingFaker = new(() =>
            new Faker<Painting>()
                .UseSeed(GetFakerSeed())
                .RuleFor(painting => painting.Title, faker => faker.Lorem.Sentence()));

        public Faker<ArtGallery> ArtGallery => _lazyArtGalleryFaker.Value;
        public Faker<Painting> Painting => _lazyPaintingFaker.Value;
    }
}
