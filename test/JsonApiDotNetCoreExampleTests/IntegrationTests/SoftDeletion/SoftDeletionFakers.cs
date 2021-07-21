using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.SoftDeletion
{
    internal sealed class SoftDeletionFakers : FakerContainer
    {
        private readonly Lazy<Faker<Company>> _lazyCompanyFaker = new(() =>
            new Faker<Company>()
                .UseSeed(GetFakerSeed())
                .RuleFor(company => company.Name, faker => faker.Company.CompanyName()));

        private readonly Lazy<Faker<Department>> _lazyDepartmentFaker = new(() =>
            new Faker<Department>()
                .UseSeed(GetFakerSeed())
                .RuleFor(department => department.Name, faker => faker.Commerce.Department()));

        public Faker<Company> Company => _lazyCompanyFaker.Value;
        public Faker<Department> Department => _lazyDepartmentFaker.Value;
    }
}
