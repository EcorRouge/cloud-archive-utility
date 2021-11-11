using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.ViewModels.Design
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
        }
    }
}
