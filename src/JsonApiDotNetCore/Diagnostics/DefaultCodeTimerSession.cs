using System;
using System.Threading;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Diagnostics
{
    /// <summary>
    /// General code timing session management. Can be used with async/wait, but it cannot distinguish between concurrently running threads, so you'll need
    /// to pass an <see cref="CascadingCodeTimer" /> instance through the entire call chain in that case.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultCodeTimerSession : ICodeTimerSession
    {
        private readonly AsyncLocal<ICodeTimer> _codeTimerInContext = new AsyncLocal<ICodeTimer>();

        public ICodeTimer CodeTimer
        {
            get
            {
                AssertNotDisposed();

                return _codeTimerInContext.Value;
            }
        }

        public event EventHandler Disposed;

        public DefaultCodeTimerSession()
        {
            _codeTimerInContext.Value = new CascadingCodeTimer();
        }

        private void AssertNotDisposed()
        {
            if (_codeTimerInContext.Value == null)
            {
                throw new ObjectDisposedException(nameof(DefaultCodeTimerSession));
            }
        }

        public void Dispose()
        {
            _codeTimerInContext.Value?.Dispose();
            _codeTimerInContext.Value = null;

            OnDisposed();
        }

        private void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
