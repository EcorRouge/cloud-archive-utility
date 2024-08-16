using System.Windows;
using System.Windows.Input;
using EcorRouge.Archive.Utility.ViewModels;

namespace EcorRouge.Archive.Utility
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ArchiveWindow : Window
    {
        public ArchiveWindow()
        {
            InitializeComponent();

            DataContext = new ArchiveWindowViewModel();
            Loaded += ArchiveWindow_Loaded;
        }

        private void ArchiveWindow_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as ArchiveWindowViewModel)?.CheckSavedState();
        }

        private void ArchiveWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
