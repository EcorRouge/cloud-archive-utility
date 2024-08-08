using EcorRouge.Archive.Utility.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EcorRouge.Archive.Utility
{
    /// <summary>
    /// Interaction logic for ExtractWindow.xaml
    /// </summary>
    public partial class ExtractWindow : Window
    {
        public ExtractWindow()
        {
            InitializeComponent();

            DataContext = new ExtractWindowViewModel();
            Loaded += ExtractWindow_Loaded;
        }

        private void ExtractWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void ExtractWindow_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as ExtractWindowViewModel)?.CheckSavedState();
        }

    }
}
