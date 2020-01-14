﻿using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class PipelineScannerOptions
    {
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);
        public IList<PipelineConfig> Pipelines { get; set; }
    }

    public class PipelineConfig
    {
        public string Project { get; set; }
        public string Name { get; set; }
        public IList<string> Branches { get; set; }
    }
}
