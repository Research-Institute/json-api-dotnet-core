using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    public sealed class IdObfuscationTests : IClassFixture<IntegrationTestContext<IdObfuscationStartup, ObfuscationDbContext>>
    {
        private readonly IntegrationTestContext<IdObfuscationStartup, ObfuscationDbContext> _testContext;
        private readonly ObfuscationFakers _fakers = new ObfuscationFakers();

        public IdObfuscationTests(IntegrationTestContext<IdObfuscationStartup, ObfuscationDbContext> testContext)
        {
            _testContext = testContext;
        }

        [Fact]
        public async Task Can_filter_equality_in_primary_resources()
        {
            // Arrange
            var bankAccounts = _fakers.BankAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BankAccount>();
                dbContext.BankAccounts.AddRange(bankAccounts);

                await dbContext.SaveChangesAsync();
            });

            var route = $"/bankAccounts?filter=equals(id,'{bankAccounts[1].StringId}')";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(bankAccounts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_any_in_primary_resources()
        {
            // Arrange
            var bankAccounts = _fakers.BankAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BankAccount>();
                dbContext.BankAccounts.AddRange(bankAccounts);

                await dbContext.SaveChangesAsync();
            });

            var route = $"/bankAccounts?filter=any(id,'{bankAccounts[1].StringId}','{HexadecimalCodec.Encode(99999999)}')";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(bankAccounts[1].StringId);
        }

        [Fact]
        public async Task Cannot_get_primary_resource_for_invalid_ID()
        {
            // Arrange
            var route = "/bankAccounts/not-a-hex-value";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.BadRequest);
            responseDocument.Errors[0].Title.Should().Be("Invalid ID value.");
            responseDocument.Errors[0].Detail.Should().Be("The value 'not-a-hex-value' is not a valid hexadecimal value.");
        }

        [Fact]
        public async Task Can_get_primary_resource_by_ID()
        {
            // Arrange
            var debitCard = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.DebitCards.Add(debitCard);
                await dbContext.SaveChangesAsync();
            });

            var route = "/debitCards/" + debitCard.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(debitCard.StringId);
        }

        [Fact]
        public async Task Can_get_secondary_resources()
        {
            // Arrange
            var bankAccount = _fakers.BankAccount.Generate();
            bankAccount.Cards = _fakers.DebitCard.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(bankAccount);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/bankAccounts/{bankAccount.StringId}/cards";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(bankAccount.Cards[0].StringId);
            responseDocument.ManyData[1].Id.Should().Be(bankAccount.Cards[1].StringId);
        }

        [Fact]
        public async Task Can_include_resource_with_sparse_fieldset()
        {
            // Arrange
            var bankAccount = _fakers.BankAccount.Generate();
            bankAccount.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(bankAccount);

                await dbContext.SaveChangesAsync();
            });

            var route = $"/bankAccounts/{bankAccount.StringId}?include=cards&fields[cards]=ownerName";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(bankAccount.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(bankAccount.Cards[0].StringId);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
        }

        [Fact]
        public async Task Can_get_relationship()
        {
            // Arrange
            var bankAccount = _fakers.BankAccount.Generate();
            bankAccount.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(bankAccount);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/bankAccounts/{bankAccount.StringId}/relationships/cards";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(bankAccount.Cards[0].StringId);
        }

        [Fact]
        public async Task Can_create_resource_with_relationship()
        {
            // Arrange
            var existingBankAccount = _fakers.BankAccount.Generate();
            var newDebitCard = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingBankAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "debitCards",
                    attributes = new
                    {
                        ownerName = newDebitCard.OwnerName,
                        pinCode = newDebitCard.PinCode
                    },
                    relationships = new
                    {
                        account = new
                        {
                            data = new
                            {
                                type = "bankAccounts",
                                id = existingBankAccount.StringId
                            }
                        }
                    }
                }
            };
            
            var route = "/debitCards";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Attributes["ownerName"].Should().Be(newDebitCard.OwnerName);
            responseDocument.SingleData.Attributes["pinCode"].Should().Be(newDebitCard.PinCode);

            var newDebitCardId = HexadecimalCodec.Decode(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var debitCardInDatabase = await dbContext.DebitCards
                    .Include(debitCard => debitCard.Account)
                    .FirstAsync(debitCard => debitCard.Id == newDebitCardId);

                debitCardInDatabase.OwnerName.Should().Be(newDebitCard.OwnerName);
                debitCardInDatabase.PinCode.Should().Be(newDebitCard.PinCode);

                debitCardInDatabase.Account.Should().NotBeNull();
                debitCardInDatabase.Account.Id.Should().Be(existingBankAccount.Id);
                debitCardInDatabase.Account.StringId.Should().Be(existingBankAccount.StringId);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_relationship()
        {
            // Arrange
            var existingBankAccount = _fakers.BankAccount.Generate();
            existingBankAccount.Cards = _fakers.DebitCard.Generate(1);

            var existingDebitCard = _fakers.DebitCard.Generate();

            var newIban = _fakers.BankAccount.Generate().Iban;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingBankAccount, existingDebitCard);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "bankAccounts",
                    id = existingBankAccount.StringId,
                    attributes = new
                    {
                        iban = newIban
                    },
                    relationships = new
                    {
                        cards = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "debitCards",
                                    id = existingDebitCard.StringId
                                }
                            }
                        }
                    }
                }
            };
            
            var route = "/bankAccounts/" + existingBankAccount.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var bankAccountInDatabase = await dbContext.BankAccounts
                    .Include(bankAccount => bankAccount.Cards)
                    .FirstAsync(bankAccount => bankAccount.Id == existingBankAccount.Id);

                bankAccountInDatabase.Iban.Should().Be(newIban);

                bankAccountInDatabase.Cards.Should().HaveCount(1);
                bankAccountInDatabase.Cards[0].Id.Should().Be(existingDebitCard.Id);
                bankAccountInDatabase.Cards[0].StringId.Should().Be(existingDebitCard.StringId);
            });

        }

        [Fact]
        public async Task Can_add_to_ToMany_relationship()
        {
            // Arrange
            var existingBankAccount = _fakers.BankAccount.Generate();
            existingBankAccount.Cards = _fakers.DebitCard.Generate(1);

            var existingDebitCard = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingBankAccount, existingDebitCard);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "debitCards",
                        id = existingDebitCard.StringId
                    }
                }
            };
            
            var route = $"/bankAccounts/{existingBankAccount.StringId}/relationships/cards";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var bankAccountInDatabase = await dbContext.BankAccounts
                    .Include(bankAccount => bankAccount.Cards)
                    .FirstAsync(bankAccount => bankAccount.Id == existingBankAccount.Id);

                bankAccountInDatabase.Cards.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_remove_from_ToMany_relationship()
        {
            // Arrange
            var existingBankAccount = _fakers.BankAccount.Generate();
            existingBankAccount.Cards = _fakers.DebitCard.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingBankAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "debitCards",
                        id = existingBankAccount.Cards[0].StringId
                    }
                }
            };
            
            var route = $"/bankAccounts/{existingBankAccount.StringId}/relationships/cards";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var bankAccountInDatabase = await dbContext.BankAccounts
                    .Include(bankAccount => bankAccount.Cards)
                    .FirstAsync(bankAccount => bankAccount.Id == existingBankAccount.Id);

                bankAccountInDatabase.Cards.Should().HaveCount(1);
            });
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            var existingBankAccount = _fakers.BankAccount.Generate();
            existingBankAccount.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingBankAccount);
                await dbContext.SaveChangesAsync();
            });

            var route = "/bankAccounts/" + existingBankAccount.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var bankAccountInDatabase = await dbContext.BankAccounts
                    .Include(bankAccount => bankAccount.Cards)
                    .FirstOrDefaultAsync(bankAccount => bankAccount.Id == existingBankAccount.Id);

                bankAccountInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_delete_missing_resource()
        {
            // Arrange
            var stringId = HexadecimalCodec.Encode(99999999);

            var route = "/bankAccounts/" + stringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.NotFound);
            responseDocument.Errors[0].Title.Should().Be("The requested resource does not exist.");
            responseDocument.Errors[0].Detail.Should().Be($"Resource of type 'bankAccounts' with ID '{stringId}' does not exist.");
            responseDocument.Errors[0].Source.Parameter.Should().BeNull();
        }
    }
}
