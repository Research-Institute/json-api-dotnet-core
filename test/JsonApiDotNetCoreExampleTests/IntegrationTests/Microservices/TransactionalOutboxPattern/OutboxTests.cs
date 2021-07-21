using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices.Messages;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices.TransactionalOutboxPattern
{
    // Implements the Transactional Outbox Microservices pattern, described at: https://microservices.io/patterns/data/transactional-outbox.html

    public sealed partial class OutboxTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OutboxDbContext>, OutboxDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OutboxDbContext>, OutboxDbContext> _testContext;
        private readonly DomainFakers _fakers = new();

        public OutboxTests(ExampleIntegrationTestContext<TestableStartup<OutboxDbContext>, OutboxDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<DomainUsersController>();
            testContext.UseController<DomainGroupsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceDefinition<OutboxUserDefinition>();
                services.AddResourceDefinition<OutboxGroupDefinition>();

                services.AddSingleton<ResourceDefinitionHitCounter>();
            });

            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            hitCounter.Reset();
        }

        [Fact]
        public async Task Does_not_add_to_outbox_on_write_error()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            DomainUser existingUser = _fakers.DomainUser.Generate();

            string missingUserId = Guid.NewGuid().ToString();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<OutgoingMessage>();
                dbContext.AddRange(existingGroup, existingUser);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "domainUsers",
                        id = existingUser.StringId
                    },
                    new
                    {
                        type = "domainUsers",
                        id = missingUserId
                    }
                }
            };

            string route = $"/domainGroups/{existingGroup.StringId}/relationships/users";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("A related resource does not exist.");
            error.Detail.Should().Be($"Related resource of type 'domainUsers' with ID '{missingUserId}' in relationship 'users' does not exist.");

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainGroup), ResourceDefinitionHitCounter.ExtensibilityPoint.OnAddToRelationshipAsync),
                (typeof(DomainGroup), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync)
            }, options => options.WithStrictOrdering());

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                List<OutgoingMessage> messages = await dbContext.OutboxMessages.OrderBy(message => message.Id).ToListAsync();
                messages.Should().BeEmpty();
            });
        }
    }
}
