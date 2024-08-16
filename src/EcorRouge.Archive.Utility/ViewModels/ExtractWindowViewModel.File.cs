using EcorRouge.Archive.Utility.CloudConnectors;
using EcorRouge.Archive.Utility.Util;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Windows.Threading;
using System.Windows;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private CancellationTokenSource _fileOpenCts;

        private bool _canBrowseFile = true;
        private bool _fileLoading;
        private ManifestFile _inputFile;

        public RelayCommand BrowseFileCommand { get; set; }

        public bool CanBrowseFile
        {
            get => _canBrowseFile;
            set => SetProperty(ref _canBrowseFile, value);
        }

        private void InitFilePage()
        {
            BrowseFileCommand = new RelayCommand(ChooseFile);
        }

        public void ChooseFile()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Supported file types|*.txt;*.csv;*.tsv;*.zip|All Files|*.*";

            if (ofd.ShowDialog() ?? false)
            {
                _fileOpenCts = new CancellationTokenSource();

                CanSelectFinish = false;
                CanBrowseFile = false;
                FileName = ofd.FileName;

                Task.Run(() =>
                {
                    _fileLoading = true;

                    TotalFilesInArchive = 0;
                    TotalFileSizeInArchive = 0;
                    
                    _inputFile = ManifestFileParser.ScanFile(FileName, null);

                    TotalFilesInArchive = _inputFile.TotalFiles;
                    TotalFileSizeInArchive = _inputFile.TotalFilesSize;

                    if(_inputFile.ContainsEncryptedCredentials) 
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ChooseKeypair();
                        });

                        if (!String.IsNullOrWhiteSpace(KeypairFileName))
                        {
                            _inputFile = ManifestFileParser.ScanFile(FileName, KeypairFileName);

                            if (!String.IsNullOrWhiteSpace(_inputFile.PluginType))
                            {
                                var pluginIndex = CloudProviders.IndexOf(_inputFile.PluginType);

                                if (pluginIndex >= 0)
                                {
                                    SelectedProviderIndex = pluginIndex;

                                    if (!String.IsNullOrWhiteSpace(_inputFile.PluginProperties))
                                    {
                                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(_inputFile.PluginProperties);

                                        for (int i = 0; i < PluginProperties.Length; i++)
                                        {
                                            if (settings.ContainsKey(PluginProperties[i].Name))
                                            {
                                                PluginProperties[i].Value = settings[PluginProperties[i].Name].ToString();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }, _fileOpenCts.Token).ContinueWith(t =>
                {
                    _fileLoading = false;
                    CanBrowseFile = true;
                    CanSelectViewFiles = TotalFilesInArchive > 0;

                    if (t.Exception != null)
                    {
                        CanSelectViewFiles = false;
                    }
                });
            }
        }
    }
}
