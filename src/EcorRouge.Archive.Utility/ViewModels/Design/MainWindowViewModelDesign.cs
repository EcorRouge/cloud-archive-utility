using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels.Design
{
    public class MainWindowViewModelDesign : MainWindowViewModel
    {
        public MainWindowViewModelDesign()
        {
            SelectedPageIndex = TAB_SELECT_FILE;

            FileName = "C:\\test.def";
            CanSelectSettings = true;

            TotalFilesToArchive = 89898;
            TotalFileSizeToArchive = 5000091823098090;

            CloudProviders.Add("Amazon S3");
            CloudProviders.Add("External HDD");

            ArchivingLabel = "Archiving: 25%, 5 files added, 12Mb(50Mb)";
            UploadingLabel = "Uploading: 37%";
            DeletingLabel = "Deleting files: 1 of 337";
            TotalLabel = "Total Progress: 1%, 37Mb processed";

            UploadingVisible = true;
            DeletingVisible = true;

            ArchiveProgress = 25.0;
            UploadProgress = 37.0;
            DeleteProgress = 1.0;
            TotalProgress = 12.0;
        }
    }
}
