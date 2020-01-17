using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestRun
    {
        public TestRun(string name, IReadOnlyList<TestAssembly> assemblies)
        {
            Name = name;
            Assemblies = assemblies;
        }

        public string Name { get; }
        public IReadOnlyList<TestAssembly> Assemblies { get; }
    }
}
