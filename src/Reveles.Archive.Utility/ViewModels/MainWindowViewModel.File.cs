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

        public bool CanBrowseFile { get; set; }
        public bool DefFileLoading { get; set; }

        public void ChooseFile()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "*.txt|Text Files|*.*|All Files";

            if (ofd.ShowDialog() ?? false)
            {
                _fileOpenCts = new CancellationTokenSource();

                CanBrowseFile = false;
                FileName = ofd.FileName;

                Task.Run(() =>
                {
                    DefFileLoading = true;

                    using (var reader = new StreamReader(FileName, Encoding.UTF8))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if(String.IsNullOrWhiteSpace(line))
                                continue;


                        }
                    }
                }).ContinueWith(t =>
                {
                    DefFileLoading = false;
                    CanBrowseFile = true;

                    if (t.Exception != null)
                    {

                    }
                });
            }
        }
    }
}
