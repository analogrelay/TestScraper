using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineTestMethod
    {
        public int Id { get; set; }
        public int AssemblyId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }

        public PipelineTestAssembly Assembly { get; set; }
        public IList<PipelineTestCase> Cases { get; set; }
    }
}
