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

        private const int MAX_MEMORY_ENTRIES = 100000;
        private List<ManifestFileEntry> _entries;

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

        private static bool MatchesSearchExpression(string path, string searchExpression)
        {
            if (String.IsNullOrWhiteSpace(searchExpression) || String.IsNullOrWhiteSpace(path))
                return true;

            return false;
        }

        //_inputFile

        private void LoadEntries()
        {
            _entries = new List<ManifestFileEntry>();

            using (var parser = ManifestFileParser.OpenFile(_inputFile, null))
            {
                ManifestFileEntry entry;
                while ((entry = parser.GetNextEntry()) != null)
                {
                    _entries.Add(entry);
                }
            }
        }

        public void UpdateSelectedFiles()
        {
            SelectedFiles.Clear();
            TotalSelectedFiles = 0;
            TotalSelectedFilesSize = 0;

            long totalSize = 0;

            if (_inputFile == null)
                return;

            if(_entries == null && _inputFile.TotalFiles < MAX_MEMORY_ENTRIES)
            {
                LoadEntries();
            }

            if(_entries == null)
            {
                using(var parser = ManifestFileParser.OpenFile(_inputFile, null))
                {
                    ManifestFileEntry entry;
                    while((entry = parser.GetNextEntry()) != null){
                        if(MatchesSearchExpression(entry.OriginalPath, SearchExpression))
                        {
                            SelectedFiles.Add(entry);
                            totalSize += entry.FileSize;
                        }
                    }
                }
            } else
            {
                foreach (var entry in _entries.Where(x => MatchesSearchExpression(x.OriginalPath, SearchExpression))) 
                {
                    SelectedFiles.Add(entry);
                    totalSize += entry.FileSize;
                }
            }

            TotalSelectedFiles = SelectedFiles.Count;
            TotalSelectedFilesSize = totalSize;
        }

        public void InitViewFiles()
        {
            
        }
    }
}
