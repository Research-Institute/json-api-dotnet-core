using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.EagerLoading
{
    internal sealed class EagerLoadingFakers : FakerContainer
    {
        private readonly Lazy<Faker<State>> _lazyStateFaker = new(() =>
            new Faker<State>()
                .UseSeed(GetFakerSeed())
                .RuleFor(state => state.Name, faker => faker.Address.City()));

        private readonly Lazy<Faker<City>> _lazyCityFaker = new(() =>
            new Faker<City>()
                .UseSeed(GetFakerSeed())
                .RuleFor(city => city.Name, faker => faker.Address.City()));

        private readonly Lazy<Faker<Street>> _lazyStreetFaker = new(() =>
            new Faker<Street>()
                .UseSeed(GetFakerSeed())
                .RuleFor(street => street.Name, faker => faker.Address.StreetName()));

        private readonly Lazy<Faker<Building>> _lazyBuildingFaker = new(() =>
            new Faker<Building>()
                .UseSeed(GetFakerSeed())
                .RuleFor(building => building.Number, faker => faker.Address.BuildingNumber()));

        private readonly Lazy<Faker<Window>> _lazyWindowFaker = new(() =>
            new Faker<Window>()
                .UseSeed(GetFakerSeed())
                .RuleFor(window => window.HeightInCentimeters, faker => faker.Random.Number(30, 199))
                .RuleFor(window => window.WidthInCentimeters, faker => faker.Random.Number(30, 199)));

        private readonly Lazy<Faker<Door>> _lazyDoorFaker = new(() =>
            new Faker<Door>()
                .UseSeed(GetFakerSeed())
                .RuleFor(door => door.Color, faker => faker.Commerce.Color()));

        public Faker<State> State => _lazyStateFaker.Value;
        public Faker<City> City => _lazyCityFaker.Value;
        public Faker<Street> Street => _lazyStreetFaker.Value;
        public Faker<Building> Building => _lazyBuildingFaker.Value;
        public Faker<Window> Window => _lazyWindowFaker.Value;
        public Faker<Door> Door => _lazyDoorFaker.Value;
    }
}
