using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Logging
{
    internal sealed class AuditFakers : FakerContainer
    {
        private readonly Lazy<Faker<AuditEntry>> _lazyAuditEntryFaker = new(() =>
            new Faker<AuditEntry>()
                .UseSeed(GetFakerSeed())
                .RuleFor(auditEntry => auditEntry.UserName, faker => faker.Internet.UserName())
                .RuleFor(auditEntry => auditEntry.CreatedAt, faker => faker.Date.PastOffset()));

        public Faker<AuditEntry> AuditEntry => _lazyAuditEntryFaker.Value;
    }
}
