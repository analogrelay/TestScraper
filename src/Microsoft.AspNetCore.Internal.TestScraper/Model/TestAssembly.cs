using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestAssembly
    {
        public TestAssembly(string name, TimeSpan? duration, DateTime? runDate, IReadOnlyList<TestError> errors, IReadOnlyList<TestCollection> collections)
        {
            Name = name;
            Duration = duration;
            RunDate = runDate;
            Errors = errors;
            Collections = collections;
        }

        public string Name { get; }
        public TimeSpan? Duration { get; }
        public DateTime? RunDate { get; }
        public IReadOnlyList<TestError> Errors { get; }
        public IReadOnlyList<TestCollection> Collections { get; }
    }
}
