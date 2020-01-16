using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestCollection
    {
        public TestCollection(string name, TimeSpan? duration, IReadOnlyList<TestResult> results)
        {
            Name = name;
            Duration = duration;
            Results = results;
        }

        public string Name { get; }
        public TimeSpan? Duration { get; }
        public IReadOnlyList<TestResult> Results { get; }
    }
}