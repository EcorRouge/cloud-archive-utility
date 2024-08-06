using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Util
{
    //"File Name", "File Size", "Created At (UTC)", "Zip File Name", "Generated File Name", "Original Path"
    public class ManifestFileEntry
    {
        public string FileName { get; set; }
        
        public long FileSize { get; set; }
        public DateTime? CreatedAtUtc { get; set; }

        public string ZipFileName  { get; set; }

        public string GeneratedFileName { get; set; }

        public string OriginalPath { get; set; }        

        public string RawEntryContent { get; set; }
    }
}
