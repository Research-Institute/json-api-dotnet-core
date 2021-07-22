using Newtonsoft.Json;

namespace TestBuildingBlocks
{
    internal sealed class IntegrationTestConfiguration
    {
        // Because our tests often deserialize incoming responses into weakly-typed string-to-object dictionaries (as part of ResourceObject),
        // Newtonsoft.JSON is unable to infer the target type in such cases. So we steer a bit using explicit configuration.
        public readonly JsonSerializerSettings DeserializationSettings = new()
        {
            // Choosing between DateTime and DateTimeOffset is impossible: it depends on how the resource properties are declared.
            // So instead we leave them as strings and let the test itself deal with the conversion.
            DateParseHandling = DateParseHandling.None,

            // Here we must choose between double (default) and decimal. Favored decimal because it has higher precision (but lower range).
            FloatParseHandling = FloatParseHandling.Decimal
        };
    }
}
