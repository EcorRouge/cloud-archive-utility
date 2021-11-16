using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private string _totalCompletedFilesLabel;
        private string _totalCompletedBytesLabel;

        public string TotalCompletedFilesLabel
        {
            get => _totalCompletedFilesLabel;
            set => SetProperty(ref _totalCompletedFilesLabel, value);
        }

        public string TotalCompletedBytesLabel
        {
            get => _totalCompletedBytesLabel;
            set => SetProperty(ref _totalCompletedBytesLabel, value);
        }
    }
}
