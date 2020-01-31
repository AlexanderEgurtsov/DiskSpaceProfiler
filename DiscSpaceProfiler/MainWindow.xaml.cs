using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DiscSpaceProfiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ScanCompleted += ehScanCompleted;
            viewModel.Run(@"C:\Addins");
        }
        void ehScanCompleted(object sender, EventArgs e)
        {
            //System.Windows.MessageBox.Show("Scan finished");
        }
    }
}
