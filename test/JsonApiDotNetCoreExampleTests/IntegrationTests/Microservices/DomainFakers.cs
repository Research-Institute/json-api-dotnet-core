using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices
{
    internal sealed class DomainFakers : FakerContainer
    {
        private readonly Lazy<Faker<DomainUser>> _lazyDomainUserFaker = new(() =>
            new Faker<DomainUser>()
                .UseSeed(GetFakerSeed())
                .RuleFor(domainUser => domainUser.LoginName, faker => faker.Person.UserName)
                .RuleFor(domainUser => domainUser.DisplayName, faker => faker.Person.FullName));

        private readonly Lazy<Faker<DomainGroup>> _lazyDomainGroupFaker = new(() =>
            new Faker<DomainGroup>()
                .UseSeed(GetFakerSeed())
                .RuleFor(domainGroup => domainGroup.Name, faker => faker.Commerce.Department()));

        public Faker<DomainUser> DomainUser => _lazyDomainUserFaker.Value;
        public Faker<DomainGroup> DomainGroup => _lazyDomainGroupFaker.Value;
    }
}
