using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.NamingConventions
{
    internal sealed class SwimmingFakers : FakerContainer
    {
        private readonly Lazy<Faker<SwimmingPool>> _lazySwimmingPoolFaker = new(() =>
            new Faker<SwimmingPool>()
                .UseSeed(GetFakerSeed())
                .RuleFor(swimmingPool => swimmingPool.IsIndoor, faker => faker.Random.Bool()));

        private readonly Lazy<Faker<WaterSlide>> _lazyWaterSlideFaker = new(() =>
            new Faker<WaterSlide>()
                .UseSeed(GetFakerSeed())
                .RuleFor(waterSlide => waterSlide.LengthInMeters, faker => faker.Random.Decimal(3, 100)));

        private readonly Lazy<Faker<DivingBoard>> _lazyDivingBoardFaker = new(() =>
            new Faker<DivingBoard>()
                .UseSeed(GetFakerSeed())
                .RuleFor(divingBoard => divingBoard.HeightInMeters, faker => faker.Random.Decimal(1, 15)));

        public Faker<SwimmingPool> SwimmingPool => _lazySwimmingPoolFaker.Value;
        public Faker<WaterSlide> WaterSlide => _lazyWaterSlideFaker.Value;
        public Faker<DivingBoard> DivingBoard => _lazyDivingBoardFaker.Value;
    }
}
