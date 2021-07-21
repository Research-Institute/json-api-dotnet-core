using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using ReportsExample.Models;

namespace ReportsExample.Services
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    public class ReportService : IGetAllService<Report>
    {
        private readonly ILogger<ReportService> _logger;

        public ReportService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ReportService>();
        }

        public Task<IReadOnlyCollection<Report>> GetAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetAsync");

            IReadOnlyCollection<Report> reports = GetReports();

            return Task.FromResult(reports);
        }

        private IReadOnlyCollection<Report> GetReports()
        {
            return new List<Report>
            {
                new()
                {
                    Title = "Status Report",
                    Statistics = new ReportStatistics
                    {
                        ProgressIndication = "Almost done",
                        HoursSpent = 24
                    }
                }
            };
        }
    }
}
