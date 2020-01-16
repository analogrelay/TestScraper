using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Rendering;
using System.Reflection;
using System.Threading.Tasks;
using TestScraper.Commands;

namespace TestScraper
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var command = new RootCommand();

            command.Add(ScrapeCommand.Create());
            command.Description = "Scrapes test results from AzDO, xUnit files, etc. and stores it in a database.";

            var builder = new CommandLineBuilder(command);
            builder.UseHelp();
            builder.UseVersionOption();
            builder.UseDebugDirective();
            builder.UseParseErrorReporting();
            builder.ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated);
            builder.UsePrefixes(new[] { "-", "--", }); // disable garbage windows conventions

            builder.CancelOnProcessTermination();
            builder.UseExceptionHandler(HandleException);

            // Allow fancy drawing.
            builder.UseAnsiTerminalWhenAvailable();

            var parser = builder.Build();
            return await parser.InvokeAsync(args);
        }

        private static void HandleException(Exception exception, InvocationContext context)
        {
            if (exception is OperationCanceledException)
            {
                context.Console.Error.WriteLine("operation canceled.");
            }
            else if (exception is TargetInvocationException tae && tae.InnerException is InvalidOperationException e)
            {
                context.Console.Error.WriteLine(e.Message);
            }
            else
            {
                context.Console.Error.WriteLine("unhandled exception: ");
                context.Console.Error.WriteLine(exception.ToString());
            }

            context.ResultCode = 1;
        }
    }
}
