using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CsvHelper;
using Microsoft.AspNetCore.Internal.TestScraper.Formats;
using Microsoft.AspNetCore.Internal.TestScraper.Model;

namespace TestScraper.Commands
{
    internal class ScrapeCommand
    {
        internal static Command Create()
        {
            var command = new Command("scrape", "Scrapes input files for test results");

            command.AddArgument(new Argument("inputPaths")
            {
                Description = "The input files/directories to scan. All files, as well as all '.xml' files in any directories (recursive) will be processed.",
                Arity = ArgumentArity.OneOrMore
            });

            command.AddOption(new Option("--platform", "The platform on which the tests ran.") { Argument = new Argument<string>() });
            command.AddOption(new Option("--configuration", "The build configuration on which the tests ran.") { Argument = new Argument<string>() });
            command.AddOption(new Option("--framework", "The target framework on which the tests ran.") { Argument = new Argument<string>() });
            command.AddOption(new Option("--output", "The output CSV file to generate.") { Argument = new Argument<string>() });

            command.Handler = CommandHandler.Create<IConsole, IList<string>, string, string, string, string, CancellationToken>(Execute);
            return command;
        }

        private static IEnumerable<string> GenerateInputFileList(IList<string> inputPaths)
        {
            foreach(var path in inputPaths)
            {
                if(File.Exists(path))
                {
                    yield return path;
                }
                else if(Directory.Exists(path))
                {
                    foreach(var file in Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
            }
        }

        private static async Task<int> Execute(IConsole console, IList<string> inputPaths, string platform, string configuration, string framework, string output, CancellationToken cancellationToken)
        {
            var rows = new List<TestResultRow>();
            foreach (var file in GenerateInputFileList(inputPaths))
            {
                console.Out.WriteLine($"Processing file: {file} ...");
                var runName = Path.GetFileNameWithoutExtension(file);

                XDocument doc;
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
                }
                var assemblies = XUnitTestResultsFormat.Parse(doc);
                foreach(var assembly in assemblies)
                {
                    foreach(var collection in assembly.Collections)
                    {
                        foreach(var result in collection.Results)
                        {
                            rows.Add(new TestResultRow()
                            {
                                Run = runName,
                                Platform = platform,
                                Configuration = configuration,
                                Framework = framework,
                                Assembly = assembly.Name,
                                Collection = collection.Name,
                                TestName = result.Name,
                                TestType = result.Type,
                                TestMethod = result.Method,
                                Traits = string.Join(';', result.Traits.Select(t => $"{t.Name}={t.Value}")),
                                Result = result.Outcome switch
                                {
                                    FailureTestOutcome _ => "fail",
                                    SuccessfulTestOutcome _ => "pass",
                                    SkippedTestOutcome _ => "skip",
                                    _ => "unknown",
                                }
                            });
                        }
                    }
                }
            }

            console.Out.WriteLine("Generating CSV file ...");
            using(var writer = new StreamWriter(output, append: false))
            using(var csv = new CsvWriter(writer))
            {
                csv.WriteRecords(rows);
            }
            console.Out.WriteLine($"Generated {output}.");

            return 0;
        }
    }

    internal class TestResultRow
    {
        public string Run { get; set; }
        public string Platform { get; set; }
        public string Configuration { get; set; }
        public string Framework { get; set; }
        public string Assembly { get; set; }
        public string Collection { get; set; }
        public string TestName { get; set; }
        public string TestType { get; set; }
        public string TestMethod { get; set; }
        public string Traits { get; set; }
        public string Result { get; set; }
    }
}
