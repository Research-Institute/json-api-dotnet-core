using System;
using FluentAssertions;
using FluentAssertions.Numeric;
using FluentAssertions.Primitives;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuildingBlocks
{
    [PublicAPI]
    public static class ObjectAssertionsExtensions
    {
        private const decimal NumericPrecision = 0.00000000001M;

        private static readonly JsonSerializerSettings DeserializationSettings = new()
        {
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// Used to assert on a (nullable) <see cref="DateTime" /> or <see cref="DateTimeOffset" /> property, whose value is returned as <see cref="string" /> in
        /// JSON:API response body because of <see cref="IntegrationTestConfiguration.DeserializationSettings" />.
        /// </summary>
        [CustomAssertion]
        public static void BeCloseTo(this ObjectAssertions source, DateTimeOffset? expected, string because = "", params object[] becauseArgs)
        {
            if (expected == null)
            {
                source.Subject.Should().BeNull(because, becauseArgs);
            }
            else
            {
                if (!DateTimeOffset.TryParse((string)source.Subject, out DateTimeOffset value))
                {
                    source.Subject.Should().Be(expected, because, becauseArgs);
                }

                // We lose a little bit of precision (milliseconds) on roundtrip through PostgreSQL database.
                value.Should().BeCloseTo(expected.Value, because: because, becauseArgs: becauseArgs);
            }
        }

        /// <summary>
        /// Same as <see cref="NumericAssertionsExtensions.BeApproximately(NumericAssertions{decimal}, decimal, decimal, string, object[])" />, but with default
        /// precision.
        /// </summary>
        [CustomAssertion]
        public static AndConstraint<NumericAssertions<decimal>> BeApproximately(this NumericAssertions<decimal> parent, decimal expectedValue,
            string because = "", params object[] becauseArgs)
        {
            return parent.BeApproximately(expectedValue, NumericPrecision, because, becauseArgs);
        }

        /// <summary>
        /// Same as <see cref="NumericAssertionsExtensions.BeApproximately(NullableNumericAssertions{decimal}, decimal?, decimal, string, object[])" />, but with
        /// default precision.
        /// </summary>
        [CustomAssertion]
        public static AndConstraint<NullableNumericAssertions<decimal>> BeApproximately(this NullableNumericAssertions<decimal> parent, decimal? expectedValue,
            string because = "", params object[] becauseArgs)
        {
            return parent.BeApproximately(expectedValue, NumericPrecision, because, becauseArgs);
        }

        /// <summary>
        /// Used to assert on a JSON-formatted string, ignoring differences in insignificant whitespace and line endings.
        /// </summary>
        [CustomAssertion]
        public static void BeJson(this StringAssertions source, string expected, string because = "", params object[] becauseArgs)
        {
            var sourceToken = JsonConvert.DeserializeObject<JToken>(source.Subject, DeserializationSettings);
            var expectedToken = JsonConvert.DeserializeObject<JToken>(expected, DeserializationSettings);

            string sourceText = sourceToken?.ToString();
            string expectedText = expectedToken?.ToString();

            sourceText.Should().Be(expectedText, because, becauseArgs);
        }
    }
}
