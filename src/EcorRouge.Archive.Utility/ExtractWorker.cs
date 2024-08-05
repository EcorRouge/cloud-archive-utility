using EcorRouge.Archive.Utility.Settings;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace EcorRouge.Archive.Utility
{
    public enum ExtractState
    {
        Initializing,
        ErrorStarting,
        Archiving,
        Uploading,
        UploadWaiting,
        Deleting,
        Completed
    }

    public class ExtractWorker
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(ExtractWorker));

        private CancellationTokenSource _cts;
        private ExtractSavedState _savedState;

        public event EventHandler Completed;
        public event EventHandler ArchivingProgress;
        public event EventHandler UploadingProgress;
        public event EventHandler DeletingProgress;
        public event EventHandler StateChanged;

        public bool IsCanceled { get; private set; }
        public bool IsBusy { get; private set; }

        public ExtractState State { get; private set; }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Start()
        {
            IsCanceled = false;

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await DoWork(_cts.Token);
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    log.Error("Error in DoWork", t.Exception);
                }

                IsCanceled = _cts.IsCancellationRequested;

                Completed?.Invoke(this, EventArgs.Empty);
            });
        }

        private void ChangeState(ExtractState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            IsBusy = true;


        }
    }
}