#pragma warning disable AV1008 // Class should not be static

namespace JsonApiDotNetCore.Diagnostics
{
    internal static class MeasurementConstants
    {
        public static readonly bool ExcludeEfCoreInPercentages = bool.Parse(bool.FalseString);
        public static readonly bool ExcludeNewtonsoftJsonInPercentages = bool.Parse(bool.FalseString);
    }
}
