using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineTestAssembly
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public IList<PipelineTestMethod> Methods { get; set; }
    }
}
