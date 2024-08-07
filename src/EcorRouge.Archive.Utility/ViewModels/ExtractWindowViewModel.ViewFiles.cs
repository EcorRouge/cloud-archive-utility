using EcorRouge.Archive.Utility.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private string _searchExpression;
        private long _totalSelectedFiles;
        private long _totalSelectedFilesSize;

        public string SearchExpression
        {
            get => _searchExpression;
            set
            {
                SetProperty(ref _searchExpression, value);
                UpdateSelectedFiles();
            }
        }

        public long TotalSelectedFiles
        {
            get => _totalSelectedFiles;
            set => SetProperty(ref _totalSelectedFiles, value);
        }

        public long TotalSelectedFilesSize
        {
            get => _totalSelectedFilesSize;
            set => SetProperty(ref _totalSelectedFilesSize, value);
        }

        public ObservableCollection<ManifestFileEntry> SelectedFiles { get; } = new ObservableCollection<ManifestFileEntry>();

        private void UpdateSelectedFiles()
        {

        }
    }
}
