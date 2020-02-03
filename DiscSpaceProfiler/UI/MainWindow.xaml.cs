using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.Code.FileSystem;
using DiscSpaceProfiler.UI.Interop;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DiscSpaceProfiler.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = this;
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

        void ScanFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Choose a folder to profile:";
            dialog.ShowNewFolderButton = false;
            if (!string.IsNullOrEmpty(ViewModel.RootPath))
                dialog.SelectedPath = ViewModel.RootPath;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            var result = dialog.ShowDialog(this.GetIWin32Window());
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            ViewModel.Run(dialog.SelectedPath);
        }

        void StopProfiling()
        {
            ViewModel?.StopProcessing();
        }

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

        private void OpenInSolutionExplorer_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            var info = TreeView?.DataControlMenu?.MenuInfo as GridCellMenuInfo;
            if (info == null)
                return;
            var fileSystemItem = info.OriginalRow as FileSystemItem;
            if (fileSystemItem == null)
                return;
            System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{fileSystemItem.GetPath()}\"");

        }
    }
}
