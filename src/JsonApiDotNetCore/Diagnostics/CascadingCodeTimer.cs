using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Diagnostics
{
    /// <summary>
    /// Records execution times for nested code blocks.
    /// </summary>
    [PublicAPI]
    public sealed class CascadingCodeTimer : ICodeTimer
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Stack<MeasureScope> _activeScopeStack = new Stack<MeasureScope>();
        private readonly List<MeasureScope> _completedScopes = new List<MeasureScope>();

        static CascadingCodeTimer()
        {
            // Be default, measurements using Stopwatch can differ 25%-30% on the same function on the same computer.
            // The steps below ensure to get an accuracy of 0.1%-0.2%. With this accuracy, algorithms can be tested and compared.
            // https://www.codeproject.com/Articles/61964/Performance-Tests-Precise-Run-Time-Measurements-wi

            // The most important thing is to prevent switching between CPU cores or processors. Switching dismisses the cache, etc. and has a huge performance impact on the test.
            Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);

            // To get the CPU core more exclusively, we must prevent that other threads can use this CPU core. We set our process and thread priority to achieve this.
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        /// <inheritdoc />
        public IDisposable Measure(string name, bool excludeInRelativeCost = false)
        {
            MeasureScope childScope = CreateChildScope(name, excludeInRelativeCost);
            _activeScopeStack.Push(childScope);

            return childScope;
        }

        private MeasureScope CreateChildScope(string name, bool excludeInRelativeCost)
        {
            if (_activeScopeStack.TryPeek(out MeasureScope topScope))
            {
                return topScope.SpawnChild(this, name, excludeInRelativeCost);
            }

            return new MeasureScope(this, name, excludeInRelativeCost);
        }

        private void Close(MeasureScope scope)
        {
            if (!_activeScopeStack.TryPeek(out MeasureScope topScope) || topScope != scope)
            {
                throw new InvalidOperationException($"Scope '{scope.Name}' cannot be disposed at this time, because it is not the currently active scope.");
            }

            _activeScopeStack.Pop();

            if (!_activeScopeStack.Any())
            {
                _completedScopes.Add(scope);
            }
        }

        /// <inheritdoc />
        public string GetResult()
        {
            var builder = new StringBuilder();

            foreach (MeasureScope scope in _completedScopes)
            {
                scope.WriteResult(builder, 0);
            }

            if (_activeScopeStack.Any())
            {
                MeasureScope scope = _activeScopeStack.Peek();
                scope.WriteResult(builder, 0);
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }

            _completedScopes.Clear();
            _activeScopeStack.Clear();
        }

        private sealed class MeasureScope : IDisposable
        {
            private readonly CascadingCodeTimer _owner;
            private readonly IList<MeasureScope> _children = new List<MeasureScope>();
            private readonly bool _excludeInRelativeCost;
            private readonly TimeSpan _startedAt;
            private TimeSpan? _stoppedAt;

            public string Name { get; }

            public MeasureScope(CascadingCodeTimer owner, string name, bool excludeInRelativeCost)
            {
                _owner = owner;
                _excludeInRelativeCost = excludeInRelativeCost;
                Name = name;

                EnsureRunning();
                _startedAt = owner._stopwatch.Elapsed;
            }

            private void EnsureRunning()
            {
                if (!_owner._stopwatch.IsRunning)
                {
                    _owner._stopwatch.Start();
                }
            }

            public MeasureScope SpawnChild(CascadingCodeTimer owner, string name, bool excludeInRelativeCost)
            {
                var childScope = new MeasureScope(owner, name, excludeInRelativeCost);
                _children.Add(childScope);
                return childScope;
            }

            private TimeSpan GetElapsedInSelf()
            {
                return GetElapsedInTotal() - GetElapsedInChildren();
            }

            private TimeSpan GetElapsedInTotal()
            {
                TimeSpan stoppedAt = _stoppedAt ?? _owner._stopwatch.Elapsed;
                return stoppedAt - _startedAt;
            }

            private TimeSpan GetElapsedInChildren()
            {
                TimeSpan elapsedInChildren = TimeSpan.Zero;

                foreach (MeasureScope childScope in _children)
                {
                    elapsedInChildren += childScope.GetElapsedInTotal();
                }

                return elapsedInChildren;
            }

            private TimeSpan GetSkippedInTotal()
            {
                TimeSpan skippedInSelf = _excludeInRelativeCost ? GetElapsedInSelf() : TimeSpan.Zero;
                TimeSpan skippedInChildren = GetSkippedInChildren();

                return skippedInSelf + skippedInChildren;
            }

            private TimeSpan GetSkippedInChildren()
            {
                TimeSpan skippedInChildren = TimeSpan.Zero;

                foreach (MeasureScope childScope in _children)
                {
                    skippedInChildren += childScope.GetSkippedInTotal();
                }

                return skippedInChildren;
            }

            public void WriteResult(StringBuilder builder, int indent)
            {
                TimeSpan timeElapsedGlobal = GetElapsedInTotal() - GetSkippedInTotal();
                WriteResult(builder, indent, timeElapsedGlobal);
            }

            private void WriteResult(StringBuilder builder, int indent, TimeSpan timeElapsedGlobal)
            {
                TimeSpan timeElapsedInSelf = GetElapsedInSelf();
                double scaleElapsedInSelf = timeElapsedGlobal != TimeSpan.Zero ? timeElapsedInSelf / timeElapsedGlobal : 0;

                WriteIndent(builder, indent);
                builder.Append(Name);
                builder.Append(": ");
                builder.Append(timeElapsedInSelf.ToString("G", CultureInfo.InvariantCulture));

                if (!_excludeInRelativeCost)
                {
                    builder.Append(" - ");
                    builder.Append(scaleElapsedInSelf.ToString("P", CultureInfo.InvariantCulture));
                }

                if (_stoppedAt == null)
                {
                    builder.Append(" (active)");
                }

                builder.AppendLine();

                foreach (MeasureScope child in _children)
                {
                    child.WriteResult(builder, indent + 1, timeElapsedGlobal);
                }
            }

            private static void WriteIndent(StringBuilder builder, int indent)
            {
                builder.Append(new string(' ', indent * 2));
            }

            public void Dispose()
            {
                if (_stoppedAt == null)
                {
                    _stoppedAt = _owner._stopwatch.Elapsed;
                    _owner.Close(this);
                }
            }
        }
    }
}
