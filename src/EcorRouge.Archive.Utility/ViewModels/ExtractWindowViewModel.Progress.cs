using EcorRouge.Archive.Utility.Settings;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private bool _canCancelProcess = false;
        private ExtractSavedState _savedState;
        private ExtractWorker _worker;

        public RelayCommand CancelProgressCommand { get; set; }

        public bool CanCancelProcess
        {
            get => _canCancelProcess;
            set => SetProperty(ref _canCancelProcess, value);
        }

        private void InitProgressPage()
        {
            CancelProgressCommand = new RelayCommand(CancelProgress);
        }

        private void CancelProgress()
        {
            if (_worker?.IsBusy ?? false)
                ConfirmInterrupt(() => { });
        }

        public void ConfirmInterrupt(Action confirmAction)
        {
            DisplayYesNoDialog(
                "Confirm interrupt",
                "Current extracting will be interrupted, do you want to continue?",
                150,
                () =>
                {
                    CanCancelProcess = false;
                    _worker?.Cancel();

                    confirmAction();
                },
                () => { }, () => { }
            );
        }

        public void StartExtracting()
        {
            StartExtracting(false);
        }

        public void StartExtracting(bool saved)
        {

        }
    }
}
