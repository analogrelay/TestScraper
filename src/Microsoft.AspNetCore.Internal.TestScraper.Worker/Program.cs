using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseSerilog((context, configuration) =>
                {
                    configuration
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("Microsoft.AspNetCore.Internal.TestScraper", LogEventLevel.Verbose)
                        .Enrich.FromLogContext();

                    var consoleLevel = context.HostingEnvironment.IsDevelopment() ?
                        LogEventLevel.Verbose :
                        LogEventLevel.Information;
                    configuration.WriteTo.Console(theme: AnsiConsoleTheme.Code, restrictedToMinimumLevel: consoleLevel);

                    var workspaceId = context.Configuration["AzureAnalytics:WorkspaceId"];
                    if(!string.IsNullOrEmpty(workspaceId))
                    {
                        configuration.WriteTo.AzureAnalytics(
                            workspaceId,
                            context.Configuration["AzureAnalytics:AuthenticationId"]);
                    }

                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<PipelineScannerOptions>(hostContext.Configuration.GetSection("PipelineScanner"));
                    services.Configure<AzDoOptions>(hostContext.Configuration.GetSection("AzDo"));

                    // We're in a hosted service so a Singleton DbContext is OK.
                    services.AddDbContext<TestResultsDbContext>(options =>
                        options.UseSqlServer(hostContext.Configuration["Sql:ConnectionString"]),
                        contextLifetime: ServiceLifetime.Singleton);

                    services.AddSingleton(services =>
                    {
                        var options = services.GetRequiredService<IOptions<AzDoOptions>>();
                        if (string.IsNullOrEmpty(options.Value.CollectionUrl))
                        {
                            throw new Exception("Missing required option: 'AzDo:CollectionUrl'");
                        }
                        if (string.IsNullOrEmpty(options.Value.PersonalAccessToken))
                        {
                            throw new Exception("Missing required option: 'AzDo:PersonalAccessToken'");
                        }
                        var creds = new VssBasicCredential(string.Empty, options.Value.PersonalAccessToken);
                        return new VssConnection(new Uri(options.Value.CollectionUrl), creds);
                    });

                    services.AddHostedService<PipelineScannerService>();
                });
        }
    }
}
