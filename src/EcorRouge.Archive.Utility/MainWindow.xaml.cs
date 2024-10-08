﻿using System;
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
    /// Interaction logic for ChooseMode.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            new ArchiveWindow().Show();
            Close();
        }

        private void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            new ExtractWindow().Show();
            Close();
        }
    }
}
