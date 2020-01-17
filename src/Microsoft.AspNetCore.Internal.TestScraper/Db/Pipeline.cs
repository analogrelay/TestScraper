using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class Pipeline: AzDoEntity
    {
        public int Id { get; set; }
        public string Project { get; set; }
        public string Name { get; set; }

        public IList<PipelineBuild> Builds { get; set; }
    }
}
