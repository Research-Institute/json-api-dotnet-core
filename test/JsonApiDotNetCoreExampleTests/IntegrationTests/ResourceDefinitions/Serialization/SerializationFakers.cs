using System;
using Bogus;
using Bogus.Extensions.UnitedStates;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceDefinitions.Serialization
{
    internal sealed class SerializationFakers : FakerContainer
    {
        private readonly Lazy<Faker<Student>> _lazyStudentFaker = new(() =>
            new Faker<Student>()
                .UseSeed(GetFakerSeed())
                .RuleFor(student => student.Name, faker => faker.Person.FullName)
                .RuleFor(student => student.SocialSecurityNumber, faker => faker.Person.Ssn()));

        private readonly Lazy<Faker<Scholarship>> _lazyScholarshipFaker = new(() =>
            new Faker<Scholarship>()
                .UseSeed(GetFakerSeed())
                .RuleFor(scholarship => scholarship.ProgramName, faker => faker.Commerce.Department())
                .RuleFor(scholarship => scholarship.Amount, faker => faker.Finance.Amount()));

        public Faker<Student> Student => _lazyStudentFaker.Value;
        public Faker<Scholarship> Scholarship => _lazyScholarshipFaker.Value;
    }
}
