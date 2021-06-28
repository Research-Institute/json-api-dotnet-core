using System;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Diagnostics
{
    /// <summary>
    /// Doesn't record anything. Intended to not break existing tests.
    /// </summary>
    [PublicAPI]
    public sealed class DisabledCodeTimer : ICodeTimer
    {
        public static readonly DisabledCodeTimer Instance = new DisabledCodeTimer();

        private DisabledCodeTimer()
        {
        }

        public IDisposable Measure(string name)
        {
            return this;
        }

        public string GetResult()
        {
            return string.Empty;
        }

        public void Dispose()
        {
        }
    }
}
