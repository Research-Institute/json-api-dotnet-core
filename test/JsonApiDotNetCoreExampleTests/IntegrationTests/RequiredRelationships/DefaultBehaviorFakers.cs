using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.RequiredRelationships
{
    internal sealed class DefaultBehaviorFakers : FakerContainer
    {
        private readonly Lazy<Faker<Order>> _orderFaker = new(() =>
            new Faker<Order>()
                .UseSeed(GetFakerSeed())
                .RuleFor(order => order.Amount, faker => faker.Finance.Amount()));

        private readonly Lazy<Faker<Customer>> _customerFaker = new(() =>
            new Faker<Customer>()
                .UseSeed(GetFakerSeed())
                .RuleFor(customer => customer.EmailAddress, faker => faker.Person.Email));

        private readonly Lazy<Faker<Shipment>> _shipmentFaker = new(() =>
            new Faker<Shipment>()
                .UseSeed(GetFakerSeed())
                .RuleFor(shipment => shipment.TrackAndTraceCode, faker => faker.Commerce.Ean13())
                .RuleFor(shipment => shipment.ShippedAt, faker => faker.Date.Past()));

        public Faker<Order> Orders => _orderFaker.Value;
        public Faker<Customer> Customers => _customerFaker.Value;
        public Faker<Shipment> Shipments => _shipmentFaker.Value;
    }
}
