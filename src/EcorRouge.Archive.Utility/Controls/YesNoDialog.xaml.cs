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
using Microsoft.Toolkit.Mvvm.Input;

namespace EcorRouge.Archive.Utility.Controls
{
    /// <summary>
    /// Interaction logic for YesNoDialog.xaml
    /// </summary>
    public partial class YesNoDialog : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(String), typeof(YesNoDialog), new FrameworkPropertyMetadata(string.Empty));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(String), typeof(YesNoDialog), new FrameworkPropertyMetadata(string.Empty));
        public static readonly DependencyProperty YesCommandProperty = DependencyProperty.Register("YesCommand", typeof(ICommand), typeof(YesNoDialog));
        public static readonly DependencyProperty NoCommandProperty = DependencyProperty.Register("NoCommand", typeof(ICommand), typeof(YesNoDialog));
        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register("CancelCommand", typeof(ICommand), typeof(YesNoDialog));

        public YesNoDialog()
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

        public ICommand YesCommand
        {
            get => GetValue(YesCommandProperty) as ICommand;
            set => SetValue(YesCommandProperty, value);
        }

        public ICommand NoCommand
        {
            get => GetValue(NoCommandProperty) as ICommand;
            set => SetValue(NoCommandProperty, value);
        }

        public ICommand CancelCommand
        {
            get => GetValue(CancelCommandProperty) as ICommand;
            set => SetValue(CancelCommandProperty, value);
        }
    }
}
