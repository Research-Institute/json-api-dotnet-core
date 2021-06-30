using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Diagnostics;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JsonApiDotNetCoreExample.Startups
{
    public sealed class Startup : EmptyStartup
    {
        private readonly string _connectionString;
        private readonly ICodeTimerSession _codeTimingSession;

        public Startup(IConfiguration configuration)
        {
            _codeTimingSession = new DefaultCodeTimerSession();
            CodeTimingSessionManager.Capture(_codeTimingSession);

            string postgresPassword = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "postgres";
            _connectionString = configuration["Data:DefaultConnection"].Replace("###", postgresPassword);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public override void ConfigureServices(IServiceCollection services)
        {
            using (CodeTimingSessionManager.Current.Measure("Configure other (startup)"))
            {
                services.AddSingleton<ISystemClock, SystemClock>();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(_connectionString);
#if DEBUG
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
#endif
                });

                using (CodeTimingSessionManager.Current.Measure("Configure JSON:API (startup)"))
                {
                    services.AddJsonApi<AppDbContext>(options =>
                    {
                        options.Namespace = "api/v1";
                        options.UseRelativeLinks = true;
                        options.ValidateModelState = true;
                        options.IncludeTotalResourceCount = true;
                        options.SerializerSettings.Formatting = Formatting.Indented;
                        options.SerializerSettings.Converters.Add(new StringEnumConverter());
#if DEBUG
                        options.IncludeExceptionStackTraceInErrors = true;
#endif
                    }, discovery => discovery.AddCurrentAssembly());
                }
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public override void Configure(IApplicationBuilder app, IWebHostEnvironment environment, ILoggerFactory loggerFactory)
        {
            ILogger<Startup> logger = loggerFactory.CreateLogger<Startup>();

            using (CodeTimingSessionManager.Current.Measure("Initialize other (startup)"))
            {
                CreateTestData(app);

                app.UseRouting();

                using (CodeTimingSessionManager.Current.Measure("Initialize JSON:API (startup)"))
                {
                    app.UseJsonApi();
                }

                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }

            string result = CodeTimingSessionManager.Current.GetResult();
            logger.LogWarning($"Measurement results for application startup:{Environment.NewLine}{result}");

            _codeTimingSession.Dispose();
        }

        private static void CreateTestData(IApplicationBuilder app)
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            appDbContext.Database.EnsureDeleted();
            appDbContext.Database.EnsureCreated();

            var tags = new List<Tag>
            {
                new Tag
                {
                    Name = "Personal"
                },
                new Tag
                {
                    Name = "Family"
                },
                new Tag
                {
                    Name = "Work"
                }
            };

            var owner = new Person
            {
                FirstName = "John",
                LastName = "Doe"
            };

            var assignee = new Person
            {
                FirstName = "Jane",
                LastName = "Doe"
            };

            for (int index = 0; index < 20; index++)
            {
                appDbContext.TodoItems.Add(new TodoItem
                {
                    Description = $"Task {index + 1}",
                    Owner = owner,
                    Assignee = assignee,
                    TodoItemTags = new HashSet<TodoItemTag>
                    {
                        new TodoItemTag
                        {
                            Tag = tags[0]
                        },
                        new TodoItemTag
                        {
                            Tag = tags[1]
                        },
                        new TodoItemTag
                        {
                            Tag = tags[2]
                        }
                    }
                });
            }

            appDbContext.SaveChanges();
        }
    }
}
