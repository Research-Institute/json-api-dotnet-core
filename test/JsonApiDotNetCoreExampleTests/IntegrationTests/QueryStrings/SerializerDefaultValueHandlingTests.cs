using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings
{
    public sealed class SerializerDefaultValueHandlingTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public SerializerDefaultValueHandlingTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<CalendarsController>();
        }

        [Fact]
        public async Task Cannot_override_from_query_string()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowQueryStringOverrideForSerializerDefaultValueHandling = false;

            const string route = "/calendars?defaults=true";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Usage of one or more query string parameters is not allowed at the requested endpoint.");
            error.Detail.Should().Be("The parameter 'defaults' cannot be used at this endpoint.");
            error.Source.Parameter.Should().Be("defaults");
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData(null, "false", false)]
        [InlineData(null, "true", true)]
        [InlineData(DefaultValueHandling.Ignore, null, false)]
        [InlineData(DefaultValueHandling.Ignore, "false", false)]
        [InlineData(DefaultValueHandling.Ignore, "true", true)]
        [InlineData(DefaultValueHandling.Include, null, true)]
        [InlineData(DefaultValueHandling.Include, "false", false)]
        [InlineData(DefaultValueHandling.Include, "true", true)]
        public async Task Can_override_from_query_string(DefaultValueHandling? configurationValue, string queryStringValue, bool expectInDocument)
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowQueryStringOverrideForSerializerDefaultValueHandling = true;
            options.SerializerSettings.DefaultValueHandling = configurationValue ?? DefaultValueHandling.Include;

            Calendar calendar = _fakers.Calendar.Generate();
            calendar.DefaultAppointmentDurationInMinutes = default;
            calendar.Appointments = _fakers.Appointment.Generate(1).ToHashSet();
            calendar.Appointments.Single().EndTime = default;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Calendars.Add(calendar);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/calendars/{calendar.StringId}?include=appointments" + (queryStringValue != null ? "&defaults=" + queryStringValue : "");

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.Included.Should().HaveCount(1);

            if (expectInDocument)
            {
                responseDocument.SingleData.Attributes.Should().ContainKey("defaultAppointmentDurationInMinutes");
                responseDocument.Included[0].Attributes.Should().ContainKey("endTime");
            }
            else
            {
                responseDocument.SingleData.Attributes.Should().NotContainKey("defaultAppointmentDurationInMinutes");
                responseDocument.Included[0].Attributes.Should().NotContainKey("endTime");
            }
        }
    }
}
