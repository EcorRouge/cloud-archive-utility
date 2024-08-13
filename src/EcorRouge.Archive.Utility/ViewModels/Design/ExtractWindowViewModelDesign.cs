using EcorRouge.Archive.Utility.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels.Design
{
    public class ExtractWindowViewModelDesign : ExtractWindowViewModel
    {
        public ExtractWindowViewModelDesign() {
            SelectedPageIndex = TAB_PROGRESS;

            ExtractingLabel = "Downloading zip file";
            TotalLabel = "Total progress: 23.50%, 12Mb, 0 errors.";
            TotalProgress = 23.5;

            DownloadingVisible = true;
            DownloadingLabel = "Downloading archive: 10% (1Mb)";
            DownloadProgress = 10;

            ExtractProgress = 5;

            CurrentFileLabel = $"test.docx (10Mb)";
        }
    }
}
