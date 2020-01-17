namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineTestResult
    {
        public int Id { get; set; }
        public int BuildId { get; set; }
        public int RunId { get; set; }
        public int CaseId { get; set; }
        public TestResultKind Result { get; set; }
        public string Traits { get; set; }
        public bool Quarantined { get; set; }
        public string QuarantinedOn { get; set; }

        public PipelineTestRun Run { get; set; }
        public PipelineTestCase Case { get; set; }
        public PipelineBuild Build { get; set; }
    }

    public enum TestResultKind
    {
        Pass,
        Fail,
        Skip,
        Unknown
    }
}