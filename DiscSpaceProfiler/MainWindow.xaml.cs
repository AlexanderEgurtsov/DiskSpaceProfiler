using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
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
        Timer updateTimer;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = this;
            updateTimer = new Timer();
            updateTimer.Interval = 2000;
            updateTimer.Elapsed += ehUpdateTreeList;
            ScanFolderCommand = new DevExpress.Mvvm.DelegateCommand(ScanFolder);
            StopProfilingCommand = new DevExpress.Mvvm.DelegateCommand(StopProfiling);
            Loaded += ehLoaded;
        }

        public ICommand ScanFolderCommand
        {
            get; private set;
        }
        public ICommand StopProfilingCommand
        {
            get; private set;
        }

        public MainWindowViewModel ViewModel { get; private set; }

        void ehLoaded(object sender, RoutedEventArgs e)
        {
            ScanFolder();
        }

        void ehUpdateTreeList(object sender, ElapsedEventArgs e)
        {
            UpdateTreeListData();
        }
        void ScanFolder()
        {
            updateTimer.Stop();
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Choose a folder to profile:";
            dialog.ShowNewFolderButton = false;
            if (!string.IsNullOrEmpty(ViewModel.RootPath))
                dialog.SelectedPath = ViewModel.RootPath;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            ViewModel.Run(dialog.SelectedPath);
            updateTimer.Start();
        }
        void StopProfiling()
        {
            ViewModel?.StopProcessing();
            UpdateTreeListData();
            updateTimer.Stop();
        }

        private void UpdateTreeListData() => DispatcherHelper.Invoke(() =>
        {
            TreeList.BeginDataUpdate();
            TreeList.EndDataUpdate();
        });

        private void TreeListView_RowDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            var node = (sender as TreeListView).GetNodeByRowHandle(e.HitInfo.RowHandle);
            if (node == null)
                return;
            if (node.IsExpanded)
                (sender as TreeListView).CollapseNode(e.HitInfo.RowHandle);
            else
                (sender as TreeListView).ExpandNode(e.HitInfo.RowHandle);
        }
    }
}
