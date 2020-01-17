using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestMethod
    {
        public TestMethod(string type, string name, IReadOnlyList<TestResult> results)
        {
            Type = type;
            Name = name;
            Results = results;
        }

        public string Type { get;  }
        public string Name { get; }
        public IReadOnlyList<TestResult> Results { get; }
    }
}