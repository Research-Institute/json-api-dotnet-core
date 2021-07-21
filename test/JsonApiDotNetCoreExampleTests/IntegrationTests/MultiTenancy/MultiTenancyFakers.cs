using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.MultiTenancy
{
    internal sealed class MultiTenancyFakers : FakerContainer
    {
        private readonly Lazy<Faker<WebShop>> _lazyWebShopFaker = new(() =>
            new Faker<WebShop>()
                .UseSeed(GetFakerSeed())
                .RuleFor(webShop => webShop.Url, faker => faker.Internet.Url()));

        private readonly Lazy<Faker<WebProduct>> _lazyWebProductFaker = new(() =>
            new Faker<WebProduct>()
                .UseSeed(GetFakerSeed())
                .RuleFor(webProduct => webProduct.Name, faker => faker.Commerce.ProductName())
                .RuleFor(webProduct => webProduct.Price, faker => faker.Finance.Amount()));

        public Faker<WebShop> WebShop => _lazyWebShopFaker.Value;
        public Faker<WebProduct> WebProduct => _lazyWebProductFaker.Value;
    }
}
