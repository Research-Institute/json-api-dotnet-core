using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Common;
using FluentAssertions.Extensions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceConstructorInjection
{
    public sealed class ResourceInjectionTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<InjectionDbContext>, InjectionDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<InjectionDbContext>, InjectionDbContext> _testContext;
        private readonly InjectionFakers _fakers;

        public ResourceInjectionTests(ExampleIntegrationTestContext<TestableStartup<InjectionDbContext>, InjectionDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<GiftCertificatesController>();
            testContext.UseController<PostOfficesController>();

            testContext.ConfigureServicesBeforeStartup(services =>
            {
                services.AddSingleton<ISystemClock, FrozenSystemClock>();
            });

            _fakers = new InjectionFakers(testContext.Factory.Services);
        }

        [Fact]
        public async Task Can_get_resource_by_ID()
        {
            // Arrange
            var clock = (FrozenSystemClock)_testContext.Factory.Services.GetRequiredService<ISystemClock>();
            clock.UtcNow = 27.January(2021);

            GiftCertificate certificate = _fakers.GiftCertificate.Generate();
            certificate.IssueDate = 28.January(2020);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.GiftCertificates.Add(certificate);
                await dbContext.SaveChangesAsync();
            });

            string route = "/giftCertificates/" + certificate.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(certificate.StringId);
            responseDocument.SingleData.Attributes["issueDate"].Should().BeCloseTo(certificate.IssueDate);
            responseDocument.SingleData.Attributes["hasExpired"].Should().Be(false);
        }

        [Fact]
        public async Task Can_filter_resources_by_ID()
        {
            // Arrange
            var clock = (FrozenSystemClock)_testContext.Factory.Services.GetRequiredService<ISystemClock>();
            clock.UtcNow = 27.January(2021).At(13, 53);

            List<PostOffice> postOffices = _fakers.PostOffice.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<PostOffice>();
                dbContext.PostOffice.AddRange(postOffices);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/postOffices?filter=equals(id,'{postOffices[1].StringId}')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(postOffices[1].StringId);
            responseDocument.ManyData[0].Attributes["address"].Should().Be(postOffices[1].Address);
            responseDocument.ManyData[0].Attributes["isOpen"].Should().Be(true);
        }

        [Fact]
        public async Task Can_get_secondary_resource_with_fieldset()
        {
            // Arrange
            var clock = (FrozenSystemClock)_testContext.Factory.Services.GetRequiredService<ISystemClock>();
            clock.UtcNow = 27.January(2021).At(13, 53);

            GiftCertificate certificate = _fakers.GiftCertificate.Generate();
            certificate.Issuer = _fakers.PostOffice.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.GiftCertificates.Add(certificate);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/giftCertificates/{certificate.StringId}/issuer?fields[postOffices]=id,isOpen";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(certificate.Issuer.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["isOpen"].Should().Be(true);
        }

        [Fact]
        public async Task Can_create_resource_with_ToOne_relationship_and_include()
        {
            // Arrange
            var clock = (FrozenSystemClock)_testContext.Factory.Services.GetRequiredService<ISystemClock>();
            clock.UtcNow = 19.March(1998).At(6, 34);

            PostOffice existingOffice = _fakers.PostOffice.Generate();

            var newIssueDate = 18.March(1997).ToDateTimeOffset();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PostOffice.Add(existingOffice);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "giftCertificates",
                    attributes = new
                    {
                        issueDate = newIssueDate
                    },
                    relationships = new
                    {
                        issuer = new
                        {
                            data = new
                            {
                                type = "postOffices",
                                id = existingOffice.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/giftCertificates?include=issuer";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["issueDate"].Should().BeCloseTo(newIssueDate);
            responseDocument.SingleData.Attributes["hasExpired"].Should().Be(true);
            responseDocument.SingleData.Relationships["issuer"].SingleData.Id.Should().Be(existingOffice.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(existingOffice.StringId);
            responseDocument.Included[0].Attributes["address"].Should().Be(existingOffice.Address);
            responseDocument.Included[0].Attributes["isOpen"].Should().Be(false);

            int newCertificateId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                GiftCertificate certificateInDatabase = await dbContext.GiftCertificates
                    .Include(certificate => certificate.Issuer).FirstWithIdAsync(newCertificateId);

                certificateInDatabase.IssueDate.Should().Be(newIssueDate);

                certificateInDatabase.Issuer.Should().NotBeNull();
                certificateInDatabase.Issuer.Id.Should().Be(existingOffice.Id);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_ToMany_relationship()
        {
            // Arrange
            var clock = (FrozenSystemClock)_testContext.Factory.Services.GetRequiredService<ISystemClock>();
            clock.UtcNow = 19.March(1998).At(6, 34);

            PostOffice existingOffice = _fakers.PostOffice.Generate();
            existingOffice.GiftCertificates = _fakers.GiftCertificate.Generate(1);

            string newAddress = _fakers.PostOffice.Generate().Address;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PostOffice.Add(existingOffice);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "postOffices",
                    id = existingOffice.StringId,
                    attributes = new
                    {
                        address = newAddress
                    },
                    relationships = new
                    {
                        giftCertificates = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "giftCertificates",
                                    id = existingOffice.GiftCertificates[0].StringId
                                }
                            }
                        }
                    }
                }
            };

            string route = "/postOffices/" + existingOffice.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                PostOffice officeInDatabase = await dbContext.PostOffice.Include(postOffice => postOffice.GiftCertificates).FirstWithIdAsync(existingOffice.Id);

                officeInDatabase.Address.Should().Be(newAddress);

                officeInDatabase.GiftCertificates.Should().HaveCount(1);
                officeInDatabase.GiftCertificates[0].Id.Should().Be(existingOffice.GiftCertificates[0].Id);
            });
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            PostOffice existingOffice = _fakers.PostOffice.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.PostOffice.Add(existingOffice);
                await dbContext.SaveChangesAsync();
            });

            string route = "/postOffices/" + existingOffice.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                PostOffice officeInDatabase = await dbContext.PostOffice.FirstWithIdOrDefaultAsync(existingOffice.Id);

                officeInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_delete_unknown_resource()
        {
            // Arrange
            const string route = "/postOffices/99999999";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be("Resource of type 'postOffices' with ID '99999999' does not exist.");
        }

        [Fact]
        public async Task Can_add_to_ToMany_relationship()
        {
            // Arrange
            PostOffice existingOffice = _fakers.PostOffice.Generate();
            existingOffice.GiftCertificates = _fakers.GiftCertificate.Generate(1);

            GiftCertificate existingCertificate = _fakers.GiftCertificate.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingOffice, existingCertificate);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "giftCertificates",
                        id = existingCertificate.StringId
                    }
                }
            };

            string route = $"/postOffices/{existingOffice.StringId}/relationships/giftCertificates";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                PostOffice officeInDatabase = await dbContext.PostOffice.Include(postOffice => postOffice.GiftCertificates).FirstWithIdAsync(existingOffice.Id);

                officeInDatabase.GiftCertificates.Should().HaveCount(2);
            });
        }
    }
}
