using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ZeroKeys
{
    internal sealed class ZeroKeyFakers : FakerContainer
    {
        private readonly Lazy<Faker<Game>> _lazyGameFaker = new(() =>
            new Faker<Game>()
                .UseSeed(GetFakerSeed())
                .RuleFor(game => game.Title, faker => faker.Random.Words()));

        private readonly Lazy<Faker<Player>> _lazyPlayerFaker = new(() =>
            new Faker<Player>()
                .UseSeed(GetFakerSeed())
                .RuleFor(player => player.Id, faker => faker.Person.UserName)
                .RuleFor(player => player.EmailAddress, faker => faker.Person.Email));

        private readonly Lazy<Faker<Map>> _lazyMapFaker = new(() =>
            new Faker<Map>()
                .UseSeed(GetFakerSeed())
                .RuleFor(map => map.Id, faker => faker.Random.Guid())
                .RuleFor(map => map.Name, faker => faker.Random.Words()));

        public Faker<Game> Game => _lazyGameFaker.Value;
        public Faker<Player> Player => _lazyPlayerFaker.Value;
        public Faker<Map> Map => _lazyMapFaker.Value;
    }
}
