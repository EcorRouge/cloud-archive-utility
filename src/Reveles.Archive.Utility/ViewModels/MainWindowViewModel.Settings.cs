using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();

        public int SelectedProviderIndex { get; set; }

        public bool DeleteFilesAfterUpload { get; set; } = true;

        public int MaximumFiles { get; set; } = 1000;

        public int MaximumArchiveSizeMb { get; set; } = 2048;
    }
}
