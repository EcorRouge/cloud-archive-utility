using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Util
{
    public class InputFile
    {
        public string FileName { get; set; }
        public long TotalFiles { get; set; }
        public long TotalFilesSize { get; set; }
        public bool IsZip { get; set; }
        public int Columns { get; set; }

        public string ConnectorPrefix { get; set; }
    }
}
