using System;

namespace EcorRouge.Archive.Utility.Util
{
    public class InputFileEntry
    {
        public string FileName { get; set; }
        public string Path { get; set; }

        public string OriginalPath { get; set; }

        public long FileSize { get; set; }

        public DateTime? CreatedAtUtc { get; set; }

        public string RawEntryContent { get; set; }
    }
}
