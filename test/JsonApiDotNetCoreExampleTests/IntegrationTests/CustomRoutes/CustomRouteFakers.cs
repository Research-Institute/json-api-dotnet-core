using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.CustomRoutes
{
    internal sealed class CustomRouteFakers : FakerContainer
    {
        private readonly Lazy<Faker<Town>> _lazyTownFaker = new(() =>
            new Faker<Town>()
                .UseSeed(GetFakerSeed())
                .RuleFor(town => town.Name, faker => faker.Address.City())
                .RuleFor(town => town.Latitude, faker => faker.Address.Latitude())
                .RuleFor(town => town.Longitude, faker => faker.Address.Longitude()));

        private readonly Lazy<Faker<Civilian>> _lazyCivilianFaker = new(() =>
            new Faker<Civilian>()
                .UseSeed(GetFakerSeed())
                .RuleFor(civilian => civilian.Name, faker => faker.Person.FullName));

        public Faker<Town> Town => _lazyTownFaker.Value;
        public Faker<Civilian> Civilian => _lazyCivilianFaker.Value;
    }
}
