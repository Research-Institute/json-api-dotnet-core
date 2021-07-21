using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    public sealed class IdObfuscationTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ObfuscationDbContext>, ObfuscationDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ObfuscationDbContext>, ObfuscationDbContext> _testContext;
        private readonly ObfuscationFakers _fakers = new();

        public IdObfuscationTests(ExampleIntegrationTestContext<TestableStartup<ObfuscationDbContext>, ObfuscationDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BankAccountsController>();
            testContext.UseController<DebitCardsController>();
        }

        [Fact]
        public async Task Can_filter_equality_in_primary_resources()
        {
            // Arrange
            List<BankAccount> accounts = _fakers.BankAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BankAccount>();
                dbContext.BankAccounts.AddRange(accounts);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/bankAccounts?filter=equals(id,'{accounts[1].StringId}')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(accounts[1].StringId);
        }

        [Fact]
        public async Task Can_filter_any_in_primary_resources()
        {
            // Arrange
            List<BankAccount> accounts = _fakers.BankAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<BankAccount>();
                dbContext.BankAccounts.AddRange(accounts);
                await dbContext.SaveChangesAsync();
            });

            var codec = new HexadecimalCodec();
            string route = $"/bankAccounts?filter=any(id,'{accounts[1].StringId}','{codec.Encode(99999999)}')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(accounts[1].StringId);
        }

        [Fact]
        public async Task Cannot_get_primary_resource_for_invalid_ID()
        {
            // Arrange
            const string route = "/bankAccounts/not-a-hex-value";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Invalid ID value.");
            error.Detail.Should().Be("The value 'not-a-hex-value' is not a valid hexadecimal value.");
        }

        [Fact]
        public async Task Can_get_primary_resource_by_ID()
        {
            // Arrange
            DebitCard card = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.DebitCards.Add(card);
                await dbContext.SaveChangesAsync();
            });

            string route = "/debitCards/" + card.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(card.StringId);
        }

        [Fact]
        public async Task Can_get_secondary_resources()
        {
            // Arrange
            BankAccount account = _fakers.BankAccount.Generate();
            account.Cards = _fakers.DebitCard.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(account);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/bankAccounts/{account.StringId}/cards";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(account.Cards[0].StringId);
            responseDocument.ManyData[1].Id.Should().Be(account.Cards[1].StringId);
        }

        [Fact]
        public async Task Can_include_resource_with_sparse_fieldset()
        {
            // Arrange
            BankAccount account = _fakers.BankAccount.Generate();
            account.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(account);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/bankAccounts/{account.StringId}?include=cards&fields[debitCards]=ownerName";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(account.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(account.Cards[0].StringId);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
            responseDocument.Included[0].Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Can_get_relationship()
        {
            // Arrange
            BankAccount account = _fakers.BankAccount.Generate();
            account.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(account);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/bankAccounts/{account.StringId}/relationships/cards";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(account.Cards[0].StringId);
        }

        [Fact]
        public async Task Can_create_resource_with_relationship()
        {
            // Arrange
            BankAccount existingAccount = _fakers.BankAccount.Generate();
            DebitCard newCard = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "debitCards",
                    attributes = new
                    {
                        ownerName = newCard.OwnerName,
                        pinCode = newCard.PinCode
                    },
                    relationships = new
                    {
                        account = new
                        {
                            data = new
                            {
                                type = "bankAccounts",
                                id = existingAccount.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/debitCards";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Attributes["ownerName"].Should().Be(newCard.OwnerName);
            responseDocument.SingleData.Attributes["pinCode"].Should().Be(newCard.PinCode);

            var codec = new HexadecimalCodec();
            int newCardId = codec.Decode(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                DebitCard cardInDatabase = await dbContext.DebitCards.Include(card => card.Account).FirstWithIdAsync(newCardId);

                cardInDatabase.OwnerName.Should().Be(newCard.OwnerName);
                cardInDatabase.PinCode.Should().Be(newCard.PinCode);

                cardInDatabase.Account.Should().NotBeNull();
                cardInDatabase.Account.Id.Should().Be(existingAccount.Id);
                cardInDatabase.Account.StringId.Should().Be(existingAccount.StringId);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_relationship()
        {
            // Arrange
            BankAccount existingAccount = _fakers.BankAccount.Generate();
            existingAccount.Cards = _fakers.DebitCard.Generate(1);

            DebitCard existingCard = _fakers.DebitCard.Generate();

            string newIban = _fakers.BankAccount.Generate().Iban;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingAccount, existingCard);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "bankAccounts",
                    id = existingAccount.StringId,
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
                                    id = existingCard.StringId
                                }
                            }
                        }
                    }
                }
            };

            string route = "/bankAccounts/" + existingAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                BankAccount accountInDatabase = await dbContext.BankAccounts.Include(account => account.Cards).FirstWithIdAsync(existingAccount.Id);

                accountInDatabase.Iban.Should().Be(newIban);

                accountInDatabase.Cards.Should().HaveCount(1);
                accountInDatabase.Cards[0].Id.Should().Be(existingCard.Id);
                accountInDatabase.Cards[0].StringId.Should().Be(existingCard.StringId);
            });
        }

        [Fact]
        public async Task Can_add_to_ToMany_relationship()
        {
            // Arrange
            BankAccount existingAccount = _fakers.BankAccount.Generate();
            existingAccount.Cards = _fakers.DebitCard.Generate(1);

            DebitCard existingDebitCard = _fakers.DebitCard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingAccount, existingDebitCard);
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

            string route = $"/bankAccounts/{existingAccount.StringId}/relationships/cards";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                BankAccount accountInDatabase = await dbContext.BankAccounts.Include(account => account.Cards).FirstWithIdAsync(existingAccount.Id);

                accountInDatabase.Cards.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_remove_from_ToMany_relationship()
        {
            // Arrange
            BankAccount existingAccount = _fakers.BankAccount.Generate();
            existingAccount.Cards = _fakers.DebitCard.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "debitCards",
                        id = existingAccount.Cards[0].StringId
                    }
                }
            };

            string route = $"/bankAccounts/{existingAccount.StringId}/relationships/cards";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                BankAccount accountInDatabase = await dbContext.BankAccounts.Include(account => account.Cards).FirstWithIdAsync(existingAccount.Id);

                accountInDatabase.Cards.Should().HaveCount(1);
            });
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            BankAccount existingAccount = _fakers.BankAccount.Generate();
            existingAccount.Cards = _fakers.DebitCard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.BankAccounts.Add(existingAccount);
                await dbContext.SaveChangesAsync();
            });

            string route = "/bankAccounts/" + existingAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                BankAccount accountInDatabase = await dbContext.BankAccounts.Include(account => account.Cards).FirstWithIdOrDefaultAsync(existingAccount.Id);

                accountInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_delete_missing_resource()
        {
            // Arrange
            var codec = new HexadecimalCodec();
            string stringId = codec.Encode(99999999);

            string route = "/bankAccounts/" + stringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be($"Resource of type 'bankAccounts' with ID '{stringId}' does not exist.");
        }
    }
}
