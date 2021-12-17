using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.Input;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private bool _showDialogShadow;
        private bool _showYesNoDialog;
        private bool _showOkDialog;
        private bool _showFolderDialog;
        private string _dialogTitle;
        private string _dialogText;

        private RelayCommand _dialogOkCommand;
        private RelayCommand _dialogYesCommand;
        private RelayCommand _dialogNoCommand;
        private RelayCommand _dialogCancelCommand;

        public RelayCommand DialogOkCommand
        {
            get => _dialogOkCommand;
            set => SetProperty(ref _dialogOkCommand, value);
        }

        public RelayCommand DialogYesCommand
        {
            get => _dialogYesCommand;
            set => SetProperty(ref _dialogYesCommand, value);
        }

        public RelayCommand DialogNoCommand
        {
            get => _dialogNoCommand;
            set => SetProperty(ref _dialogNoCommand, value);
        }

        public RelayCommand DialogCancelCommand
        {
            get => _dialogCancelCommand;
            set => SetProperty(ref _dialogCancelCommand, value);
        }

        public bool ShowDialogShadow
        {
            get => _showDialogShadow;
            set => SetProperty(ref _showDialogShadow, value);
        }

        public bool ShowOkDialog
        {
            get => _showOkDialog;
            set => SetProperty(ref _showOkDialog, value);
        }

        public bool ShowYesNoDialog
        {
            get => _showYesNoDialog;
            set => SetProperty(ref _showYesNoDialog, value);
        }

        public bool ShowFolderDialog
        {
            get => _showFolderDialog;
            set => SetProperty(ref _showFolderDialog, value);
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string DialogText
        {
            get => _dialogText;
            set => SetProperty(ref _dialogText, value);
        }

        public void DisplayYesNoDialog(string title, string text, Action yesCallback, Action noCallback, Action cancelCallback)
        {
            ShowDialogShadow = true;
            ShowYesNoDialog = true;
            ShowOkDialog = false;

            DialogTitle = title;
            DialogText = text;

            DialogYesCommand = new RelayCommand(() =>
            {
                ShowDialogShadow = false;
                ShowYesNoDialog = false;

                yesCallback();
            });

            DialogNoCommand = new RelayCommand(() =>
            {
                ShowDialogShadow = false;
                ShowYesNoDialog = false;

                noCallback();
            });

            DialogCancelCommand = new RelayCommand(() =>
            {
                ShowDialogShadow = false;
                ShowYesNoDialog = false;

                cancelCallback();
            });
        }

        public void DisplayOkDialog(string title, string text, Action okCallback)
        {
            ShowDialogShadow = true;
            ShowYesNoDialog = false;
            ShowOkDialog = true;

            DialogTitle = title;
            DialogText = text;

            DialogOkCommand = new RelayCommand(() =>
            {
                ShowDialogShadow = false;
                ShowOkDialog = false;

                okCallback();
            });
        }
    }
}
