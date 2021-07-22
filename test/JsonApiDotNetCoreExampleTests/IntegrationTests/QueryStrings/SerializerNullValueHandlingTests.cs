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
    public sealed class SerializerNullValueHandlingTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> _testContext;
        private readonly QueryStringFakers _fakers = new();

        public SerializerNullValueHandlingTests(ExampleIntegrationTestContext<TestableStartup<QueryStringDbContext>, QueryStringDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<CalendarsController>();
        }

        [Fact]
        public async Task Cannot_override_from_query_string()
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowQueryStringOverrideForSerializerNullValueHandling = false;

            const string route = "/calendars?nulls=true";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Usage of one or more query string parameters is not allowed at the requested endpoint.");
            error.Detail.Should().Be("The parameter 'nulls' cannot be used at this endpoint.");
            error.Source.Parameter.Should().Be("nulls");
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData(null, "false", false)]
        [InlineData(null, "true", true)]
        [InlineData(NullValueHandling.Ignore, null, false)]
        [InlineData(NullValueHandling.Ignore, "false", false)]
        [InlineData(NullValueHandling.Ignore, "true", true)]
        [InlineData(NullValueHandling.Include, null, true)]
        [InlineData(NullValueHandling.Include, "false", false)]
        [InlineData(NullValueHandling.Include, "true", true)]
        public async Task Can_override_from_query_string(NullValueHandling? configurationValue, string queryStringValue, bool expectInDocument)
        {
            // Arrange
            var options = (JsonApiOptions)_testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowQueryStringOverrideForSerializerNullValueHandling = true;
            options.SerializerSettings.NullValueHandling = configurationValue ?? NullValueHandling.Include;

            Calendar calendar = _fakers.Calendar.Generate();
            calendar.TimeZone = null;
            calendar.Appointments = _fakers.Appointment.Generate(1).ToHashSet();
            calendar.Appointments.Single().Title = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Calendars.Add(calendar);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/calendars/{calendar.StringId}?include=appointments" + (queryStringValue != null ? "&nulls=" + queryStringValue : "");

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.Included.Should().HaveCount(1);

            if (expectInDocument)
            {
                responseDocument.SingleData.Attributes.Should().ContainKey("timeZone");
                responseDocument.Included[0].Attributes.Should().ContainKey("title");
            }
            else
            {
                responseDocument.SingleData.Attributes.Should().NotContainKey("timeZone");
                responseDocument.Included[0].Attributes.Should().NotContainKey("title");
            }
        }
    }
}
