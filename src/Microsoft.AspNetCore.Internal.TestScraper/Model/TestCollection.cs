using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestCollection
    {
        public TestCollection(string name, TimeSpan? duration, IReadOnlyList<TestMethod> methods)
        {
            Name = name;
            Duration = duration;
            Methods = methods;
        }

        public string Name { get; }
        public TimeSpan? Duration { get; }
        public IReadOnlyList<TestMethod> Methods { get; }
    }
}