using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestRun
    {
        public TestRun(string name, string platform, string configuration, string framework, IReadOnlyList<TestAssembly> assemblies)
        {
            Name = name;
            Platform = platform;
            Configuration = configuration;
            Framework = framework;
            Assemblies = assemblies;
        }

        public string Name { get; }
        public string Platform { get; }
        public string Configuration { get; }
        public string Framework { get; }
        public IReadOnlyList<TestAssembly> Assemblies { get; }
    }
}
