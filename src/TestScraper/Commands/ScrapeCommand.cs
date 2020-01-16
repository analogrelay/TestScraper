using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace TestScraper.Commands
{
    internal class ScrapeCommand
    {
        internal static Command Create()
        {
            var command = new Command("scrape", "Scrapes input files for test results");

            command.AddArgument(new Argument("inputFile")
            {
                Description = "The input file to scrape",
                Arity = ArgumentArity.OneOrMore
            });

            command.Handler = CommandHandler.Create<IConsole, IList<string>>(Execute);
            return command;
        }

        private static async Task<int> Execute(IConsole console, IList<string> inputFile)
        {
        }
    }
}
