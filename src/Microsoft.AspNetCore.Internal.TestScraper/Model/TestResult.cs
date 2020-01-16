using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestResult
    {
        public TestResult(string name, string type, string method, TimeSpan? duration, TestOutcome outcome, IReadOnlyList<TestTrait> traits)
        {
            Name = name;
            Type = type;
            Method = method;
            Duration = duration;
            Outcome = outcome;
            Traits = traits;
        }

        public string Name { get; }
        public string Type { get; }
        public string Method { get; }
        public TimeSpan? Duration { get; }
        public TestOutcome Outcome { get; }
        public IReadOnlyList<TestTrait> Traits { get; }
    }
}