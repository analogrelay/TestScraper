namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineTestResult
    {
        public int Id { get; set; }
        public string Run { get; set; }
        public string Collection { get; set; }
        public string Assembly { get; set; }
        public string Type { get; set; }
        public string Method { get; set; }
        public string FullName { get; set; }
        public TestResultKind Result { get; set; }
        public string SkipReason { get; set; }
        public string FailureMessage { get; set; }
        public string FailureStackTrace { get; set; }

        public PipelineBuild Build { get; set; }
    }

    public enum TestResultKind
    {
        Pass,
        Fail,
        Skip
    }
}