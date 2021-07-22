using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Archiving
{
    internal sealed class TelevisionFakers : FakerContainer
    {
        private readonly Lazy<Faker<TelevisionNetwork>> _lazyTelevisionNetworkFaker = new(() =>
            new Faker<TelevisionNetwork>()
                .UseSeed(GetFakerSeed())
                .RuleFor(network => network.Name, faker => faker.Company.CompanyName()));

        private readonly Lazy<Faker<TelevisionStation>> _lazyTelevisionStationFaker = new(() =>
            new Faker<TelevisionStation>()
                .UseSeed(GetFakerSeed())
                .RuleFor(station => station.Name, faker => faker.Company.CompanyName()));

        private readonly Lazy<Faker<TelevisionBroadcast>> _lazyTelevisionBroadcastFaker = new(() =>
            new Faker<TelevisionBroadcast>()
                .UseSeed(GetFakerSeed())
                .RuleFor(broadcast => broadcast.Title, faker => faker.Lorem.Sentence())
                .RuleFor(broadcast => broadcast.AiredAt, faker => faker.Date.PastOffset())
                .RuleFor(broadcast => broadcast.ArchivedAt, faker => faker.Date.RecentOffset()));

        private readonly Lazy<Faker<BroadcastComment>> _lazyBroadcastCommentFaker = new(() =>
            new Faker<BroadcastComment>()
                .UseSeed(GetFakerSeed())
                .RuleFor(comment => comment.Text, faker => faker.Lorem.Paragraph())
                .RuleFor(comment => comment.CreatedAt, faker => faker.Date.PastOffset()));

        public Faker<TelevisionNetwork> TelevisionNetwork => _lazyTelevisionNetworkFaker.Value;
        public Faker<TelevisionStation> TelevisionStation => _lazyTelevisionStationFaker.Value;
        public Faker<TelevisionBroadcast> TelevisionBroadcast => _lazyTelevisionBroadcastFaker.Value;
        public Faker<BroadcastComment> BroadcastComment => _lazyBroadcastCommentFaker.Value;
    }
}
