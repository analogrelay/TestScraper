using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public abstract class AzDoEntity
    {
        public int AzDoId { get; set; }
        public string WebUrl { get; set; }
    }
}
