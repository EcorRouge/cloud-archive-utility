﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Util
{
    public class InputFileEntry
    {
        public string FileName { get; set; }
        public string Path { get; set; }
        public long FileSize { get; set; }

        public DateTime? CreatedAtUtc { get; set; }
    }
}
