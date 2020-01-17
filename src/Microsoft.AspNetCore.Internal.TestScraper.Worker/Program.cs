using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<PipelineScannerOptions>(hostContext.Configuration.GetSection("PipelineScanner"));
                    services.Configure<AzDoOptions>(hostContext.Configuration.GetSection("AzDo"));

                    services.AddDbContext<TestResultsDbContext>(options =>
                        options.UseSqlServer(hostContext.Configuration["Sql:ConnectionString"]));
                    
                    services.AddSingleton(services =>
                    {
                        var options = services.GetRequiredService<IOptions<AzDoOptions>>();
                        if(string.IsNullOrEmpty(options.Value.CollectionUrl))
                        {
                            throw new Exception("Missing required option: 'AzDo:CollectionUrl'");
                        }
                        if(string.IsNullOrEmpty(options.Value.PersonalAccessToken))
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
