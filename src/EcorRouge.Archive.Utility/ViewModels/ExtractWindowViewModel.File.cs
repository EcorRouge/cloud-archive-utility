﻿using EcorRouge.Archive.Utility.CloudConnectors;
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

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private CancellationTokenSource _fileOpenCts;

        private bool _canBrowseFile = true;
        private bool _fileLoading;
        private InputFile _inputFile;

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

                    TotalFilesToArchive = 0;
                    TotalFileSizeToArchive = 0;

                    /*
                    _inputFile = InputFileParser.ScanFile(FileName, connectorsMarkers);

                    TotalFilesToArchive = _inputFile.TotalFiles;
                    TotalFileSizeToArchive = _inputFile.TotalFilesSize;
                    */

                    //TODO: Scan manifest!
                }, _fileOpenCts.Token).ContinueWith(t =>
                {
                    _fileLoading = false;
                    CanBrowseFile = true;
                    CanSelectSettings = TotalFilesToArchive > 0;

                    if (t.Exception != null)
                    {
                        CanSelectSettings = false;
                    }
                });
            }
        }
    }
}
