using Microsoft.AspNetCore.Internal.TestScraper.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.AspNetCore.Internal.TestScraper.Formats
{
    public static class XUnitTestResultsFormat
    {
        public static IReadOnlyList<TestAssembly> Parse(XDocument document)
        {
            var assemblies = document.Elements("assemblies") ?? throw new FormatException("Missing expected root element '<assemblies>'");

            return ParseList(ParseAssembly, assemblies.Elements("assembly"));
        }

        private static TestAssembly ParseAssembly(XElement assembly)
        {
            return new TestAssembly(
                name: assembly.Attribute("name")?.Value,
                duration: ParseTime(assembly.Attribute("time")?.Value),
                runDate: ParseDate(assembly.Attribute("run-date")?.Value, assembly.Attribute("run-time")?.Value),
                errors: ParseList(ParseError, assembly.Element("errors"), elementName: "error"),
                collections: ParseList(ParseCollection, assembly.Elements("collection")));
        }

        private static DateTime? ParseDate(string runDate, string runTime)
        {
            if (DateTime.TryParse(runDate, out var dateResult) && TimeSpan.TryParse(runTime, out var timeResult))
            {
                return dateResult.Add(timeResult);
            }
            return null;
        }

        private static TestError ParseError(XElement error)
        {
            return new TestError(
                type: error.Attribute("type")?.Value,
                name: error.Attribute("name")?.Value);
        }

        private static TestCollection ParseCollection(XElement collection)
        {
            var groupedResults = collection.Elements("test").GroupBy(t => new { Type = t.Attribute("type")?.Value, Method = t.Attribute("method")?.Value });

            var methods = groupedResults.Select(g => new TestMethod(g.Key.Type, g.Key.Method, results: g.Select(ParseResult).ToList())).ToList();

            return new TestCollection(
                name: collection.Attribute("name")?.Value,
                duration: ParseTime(collection.Attribute("time")?.Value),
                methods: methods);
        }

        private static TestResult ParseResult(XElement result)
        {
            return new TestResult(
                name: result.Attribute("name")?.Value,
                duration: ParseTime(result.Attribute("time")?.Value),
                outcome: result.Attribute("result")?.Value.ToLowerInvariant() switch
                {
                    "pass" => SuccessfulTestOutcome.Instance,
                    "fail" => ParseFailure(result.Element("failure")),
                    "skip" => new SkippedTestOutcome(result.Element("reason")?.Value),
                    _ => null,
                },
                traits: ParseList(ParseTrait, result.Element("traits"), "trait"));
        }

        private static TimeSpan? ParseTime(string value)
        {
            if (!string.IsNullOrEmpty(value) && double.TryParse(value, out var valueSeconds))
            {
                return TimeSpan.FromSeconds(valueSeconds);
            }
            return null;
        }

        // Keeping this return value "TestOutcome" means the switch expression can find a "best type".
        private static TestOutcome ParseFailure(XElement failure)
        {
            if (failure == null)
            {
                return null;
            }

            return new FailureTestOutcome(
                message: failure.Element("message")?.Value,
                stackTrace: failure.Element("stack-trace")?.Value);
        }

        private static TestTrait ParseTrait(XElement trait)
        {
            return new TestTrait(
                name: trait.Attribute("name")?.Value,
                value: trait.Attribute("value")?.Value);
        }

        private static List<T> ParseList<T>(Func<XElement, T> parser, IEnumerable<XElement> elements)
        {
            var list = new List<T>();
            foreach (var child in elements)
            {
                list.Add(parser(child));
            }
            return list;
        }

        private static List<T> ParseList<T>(Func<XElement, T> parser, XElement parent, string elementName)
        {
            var elements = parent?.Elements(elementName) ?? Enumerable.Empty<XElement>();
            return ParseList<T>(parser, elements);
        }
    }
}
