using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineBuild: AzDoEntity
    {
        public int Id { get; set; }
        public int PipelineId { get; set; }
        public string BuildNumber { get; set; }
        public string SourceBranch { get; set; }
        public string SourceVersion { get; set; }
        public SyncStatus SyncStatus { get; set; }
        public int SyncAttempts { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? CompletedTimeUtc { get; set; }
        public DateTime? SyncStartedUtc { get; set; }
        public DateTime? SyncCompleteUtc { get; set; }
        public PipelineBuildResult? Result { get; set; }

        public Pipeline Pipeline { get; set; }
        public IList<PipelineTestResult> TestResults { get; set; }
    }

    public enum SyncStatus
    {
        NotStarted,
        InProgress,
        Failed,
        Complete,
        Cancelled,
    }

    public enum PipelineBuildResult
    {
        Succeeded,
        PartiallySucceeded,
        Failed,
        Canceled
    }
}
