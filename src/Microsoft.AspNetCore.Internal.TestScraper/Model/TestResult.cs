using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestResult
    {
        public TestResult(string name, TimeSpan? duration, TestOutcome outcome, IReadOnlyList<TestTrait> traits)
        {
            Name = name;
            Duration = duration;
            Outcome = outcome;
            Traits = traits;
        }

        public string Name { get; }
        public TimeSpan? Duration { get; }
        public TestOutcome Outcome { get; }
        public IReadOnlyList<TestTrait> Traits { get; }
    }
}