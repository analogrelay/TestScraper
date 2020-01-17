using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineBuild: AzDoEntity
    {
        public int Id { get; set; }
        public int PipelineId { get; set; }

        public Pipeline Pipeline { get; set; }
        public IList<PipelineTestResult> TestResults { get; set; }
    }
}
