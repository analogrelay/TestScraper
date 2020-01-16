namespace Microsoft.AspNetCore.Internal.TestScraper.Model
{
    public class TestTrait
    {
        public TestTrait(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }
    }
}