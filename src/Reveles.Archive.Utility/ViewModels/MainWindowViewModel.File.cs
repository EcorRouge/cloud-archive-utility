using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Reveles.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private CancellationTokenSource _fileOpenCts;

        public bool CanBrowseFile { get; set; } = true;
        public bool DefFileLoading { get; set; }

        public void ChooseFile()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Text Files|*.txt|All Files|*.*";

            if (ofd.ShowDialog() ?? false)
            {
                _fileOpenCts = new CancellationTokenSource();

                CanBrowseFile = false;
                FileName = ofd.FileName;

                Task.Run(() =>
                {
                    DefFileLoading = true;

                    TotalFilesToArchive = 0;
                    TotalFileSizeToArchive = 0;

                    using (var reader = new StreamReader(FileName, Encoding.UTF8))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if(String.IsNullOrWhiteSpace(line))
                                continue;

                            //{file.FileName}|{file.FileLength}|{file.LastWriteTime}
                            var parts = line.Split("|");
                            if(parts.Length < 3)
                                continue;

                            long length = 0;

                            if (Int64.TryParse(parts[1], out length))
                            {
                                TotalFilesToArchive++;
                                TotalFileSizeToArchive += length;
                            }
                        }
                    }
                }, _fileOpenCts.Token).ContinueWith(t =>
                {
                    DefFileLoading = false;
                    CanBrowseFile = true;
                    CanSelectSettings = TotalFilesToArchive > 0 && TotalFileSizeToArchive > 0;

                    if (t.Exception != null)
                    {
                        CanSelectSettings = false;
                    }
                });
            }
        }
    }
}
