using System;
using System.Linq;
using JetBrains.Annotations;

#pragma warning disable AV1008 // Class should not be static

namespace JsonApiDotNetCore.Diagnostics
{
    /// <summary>
    /// Provides access to the "current" measurement, which removes the need to pass along a <see cref="CascadingCodeTimer" /> instance through the entire
    /// call chain.
    /// </summary>
    [PublicAPI]
    public static class CodeTimingSessionManager
    {
        private static bool _isRunningInTest;
        private static ICodeTimerSession _session;

        public static ICodeTimer Current
        {
            get
            {
                if (_isRunningInTest)
                {
                    return DisabledCodeTimer.Instance;
                }

                AssertHasActiveSession();

                return _session.CodeTimer;
            }
        }

        static CodeTimingSessionManager()
        {
            const string testAssemblyName = "xunit.core";

            _isRunningInTest = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                assembly.FullName != null && assembly.FullName.StartsWith(testAssemblyName, StringComparison.Ordinal));
        }

        private static void AssertHasActiveSession()
        {
            if (_session == null)
            {
                throw new InvalidOperationException($"Call {nameof(Capture)} before accessing the current session.");
            }
        }

        public static void Capture(ICodeTimerSession session)
        {
            ArgumentGuard.NotNull(session, nameof(session));

            AssertNoActiveSession();

            if (!_isRunningInTest)
            {
                session.Disposed += SessionOnDisposed;
                _session = session;
            }
        }

        private static void AssertNoActiveSession()
        {
            if (_session != null)
            {
                throw new InvalidOperationException("Sessions cannot be nested. Dispose the current session first.");
            }
        }

        private static void SessionOnDisposed(object sender, EventArgs args)
        {
            if (_session != null)
            {
                _session.Disposed -= SessionOnDisposed;
                _session = null;
            }
        }
    }
}
