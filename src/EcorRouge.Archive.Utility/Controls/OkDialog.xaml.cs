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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EcorRouge.Archive.Utility.Controls
{
    /// <summary>
    /// Interaction logic for OkDialog.xaml
    /// </summary>
    public partial class OkDialog : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(String), typeof(OkDialog), new FrameworkPropertyMetadata(string.Empty));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(String), typeof(OkDialog), new FrameworkPropertyMetadata(string.Empty));
        public static readonly DependencyProperty OkCommandProperty = DependencyProperty.Register("OkCommand", typeof(ICommand), typeof(OkDialog));

        public OkDialog()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => GetValue(TextProperty).ToString();
            set => SetValue(TextProperty, value);
        }

        public string Title
        {
            get => GetValue(TitleProperty).ToString();
            set => SetValue(TitleProperty, value);
        }

        public ICommand OkCommand
        {
            get => GetValue(OkCommandProperty) as ICommand;
            set => SetValue(OkCommandProperty, value);
        }
    }
}
