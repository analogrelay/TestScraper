namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public abstract class TestOutcome
    {
    }

    public class SuccessfulTestOutcome: TestOutcome
    {
        public static readonly SuccessfulTestOutcome Instance = new SuccessfulTestOutcome();

        private SuccessfulTestOutcome()
        {
        }
    }

    public class FailureTestOutcome: TestOutcome
    {
        public FailureTestOutcome(string message, string stackTrace)
        {
            Message = message;
            StackTrace = stackTrace;
        }

        public string Message { get; }
        public string StackTrace { get; }
    }

    public class SkippedTestOutcome: TestOutcome
    {
        public SkippedTestOutcome(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }
}