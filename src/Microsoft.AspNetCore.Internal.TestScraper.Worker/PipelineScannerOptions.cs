using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class PipelineScannerOptions
    {
        public bool TagScrapedBuilds { get; set; }
        public int MinFinishTimeInDaysAgo { get; set; }
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);
        public IList<PipelineConfig> Pipelines { get; set; }
        public int MaxSyncAttempts { get; set; } = 3;
    }

    public class PipelineConfig
    {
        public string Project { get; set; }
        public string Name { get; set; }
        public IList<string> Branches { get; set; }
        public IList<string> ArtifactPatterns { get; set; }
    }
}
