using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    internal sealed class ObfuscationFakers : FakerContainer
    {
        private readonly Lazy<Faker<BankAccount>> _lazyBankAccountFaker = new(() =>
            new Faker<BankAccount>()
                .UseSeed(GetFakerSeed())
                .RuleFor(bankAccount => bankAccount.Iban, faker => faker.Finance.Iban()));

        private readonly Lazy<Faker<DebitCard>> _lazyDebitCardFaker = new(() =>
            new Faker<DebitCard>()
                .UseSeed(GetFakerSeed())
                .RuleFor(debitCard => debitCard.OwnerName, faker => faker.Name.FullName())
                .RuleFor(debitCard => debitCard.PinCode, faker => (short)faker.Random.Number(1000, 9999)));

        public Faker<BankAccount> BankAccount => _lazyBankAccountFaker.Value;
        public Faker<DebitCard> DebitCard => _lazyDebitCardFaker.Value;
    }
}
