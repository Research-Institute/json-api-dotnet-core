#if DEBUG

using System;
using System.Threading.Tasks;

namespace JsonApiDotNetCore.Diagnostics
{
    public class ConsoleDemo
    {
        public static async Task RunAsync()
        {
            using var session = new DefaultCodeTimerSession();
            CodeTimingSessionManager.Capture(session);

            await ProcessRequestAsync();

            string result = CodeTimingSessionManager.Current.GetResult();
            Console.WriteLine(result);
        }

        private static async Task ProcessRequestAsync()
        {
            using (CodeTimingSessionManager.Current.Measure(nameof(ProcessRequestAsync)))
            {
                await ParseQueryStringAsync();

                await Task.Delay(TimeSpan.FromSeconds(1.3));

                await FetchDataFromDbAsync();

                await SerializeResponseAsync();

                await Task.Delay(TimeSpan.FromSeconds(.7));
            }
        }

        private static async Task ParseQueryStringAsync()
        {
            using (CodeTimingSessionManager.Current.Measure(nameof(ParseQueryStringAsync)))
            {
                await Task.Delay(TimeSpan.FromSeconds(.5));
            }
        }

        private static async Task FetchDataFromDbAsync()
        {
            using (CodeTimingSessionManager.Current.Measure(nameof(FetchDataFromDbAsync)))
            {
                await ExecuteSqlQueryAsync(TimeSpan.FromSeconds(1));

                await Task.Delay(TimeSpan.FromSeconds(.1));
                Console.WriteLine(CodeTimingSessionManager.Current.GetResult());

                await ExecuteSqlQueryAsync(TimeSpan.FromSeconds(2));
            }
        }

        private static async Task ExecuteSqlQueryAsync(TimeSpan sleepTime)
        {
            using (CodeTimingSessionManager.Current.Measure(nameof(ExecuteSqlQueryAsync)))
            {
                await Task.Delay(sleepTime);
            }
        }

        private static async Task SerializeResponseAsync()
        {
            using (CodeTimingSessionManager.Current.Measure(nameof(SerializeResponseAsync)))
            {
                await Task.Delay(TimeSpan.FromSeconds(.25));
            }
        }
    }
}

#endif
