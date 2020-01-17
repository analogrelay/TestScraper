namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class PipelineTestCase
    {
        public int Id { get; set; }
        public int MethodId { get; set; }
        public string Name { get; set; }

        public PipelineTestMethod Method { get; set; }
    }
}
