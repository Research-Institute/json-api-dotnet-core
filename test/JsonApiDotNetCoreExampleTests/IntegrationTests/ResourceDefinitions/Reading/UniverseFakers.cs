using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceDefinitions.Reading
{
    internal sealed class UniverseFakers : FakerContainer
    {
        private readonly Lazy<Faker<Star>> _lazyStarFaker = new(() =>
            new Faker<Star>()
                .UseSeed(GetFakerSeed())
                .RuleFor(star => star.Name, faker => faker.Random.Word())
                .RuleFor(star => star.Kind, faker => faker.PickRandom<StarKind>())
                .RuleFor(star => star.SolarRadius, faker => faker.Random.Decimal(.01M, 1000M))
                .RuleFor(star => star.SolarMass, faker => faker.Random.Decimal(.001M, 100M))
                .RuleFor(star => star.IsVisibleFromEarth, faker => faker.Random.Bool()));

        private readonly Lazy<Faker<Planet>> _lazyPlanetFaker = new(() =>
            new Faker<Planet>()
                .UseSeed(GetFakerSeed())
                .RuleFor(planet => planet.PublicName, faker => faker.Random.Word())
                .RuleFor(planet => planet.HasRingSystem, faker => faker.Random.Bool())
                .RuleFor(planet => planet.SolarMass, faker => faker.Random.Decimal(.001M, 100M)));

        private readonly Lazy<Faker<Moon>> _lazyMoonFaker = new(() =>
            new Faker<Moon>()
                .UseSeed(GetFakerSeed())
                .RuleFor(moon => moon.Name, faker => faker.Random.Word())
                .RuleFor(moon => moon.SolarRadius, faker => faker.Random.Decimal(.01M, 1000M)));

        public Faker<Star> Star => _lazyStarFaker.Value;
        public Faker<Planet> Planet => _lazyPlanetFaker.Value;
        public Faker<Moon> Moon => _lazyMoonFaker.Value;
    }
}
