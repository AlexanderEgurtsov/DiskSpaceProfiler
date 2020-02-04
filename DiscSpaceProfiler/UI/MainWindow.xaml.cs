using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.Code.FileSystem;
using DiscSpaceProfiler.UI.Interop;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Generic;
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
        public static TreeListControl TreeListControl;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            ViewModel.FileSystemItemAdded += ehFileSystemItemAdded;
            ViewModel.FileSystemItemDeleted += ehFileSystemItemDeleted;
            ViewModel.FileSystemItemProcessed += ehFileSystemItemProcessed;
            ViewModel.FolderSizeCalculated += ehFolderSizeCalculated;
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

        public void ScanFolder(string path)
        {
            TreeListControl = TreeList;
            TreeList.View.Nodes.Clear();
            ViewModel.Run(path);
            TreeList.View.Nodes.Add(CreateNodeForItem(ViewModel.RootNode));
        }
        TreeListNode CreateNodeForItem(FileSystemItem item)
        {
            var newNode = new TreeListNode(item);
            item.VisualObject = newNode;
            if (item is FolderItem nestedFolder)
                newNode.IsExpandButtonVisible = nestedFolder.HasChildren ? DevExpress.Utils.DefaultBoolean.True : DevExpress.Utils.DefaultBoolean.False;
            return newNode;
        }
        private void ehFileSystemItemAdded(object sender, FileSystemItemChangedEventArgs e)
        {
            var parentNode = e?.Parent?.VisualObject as TreeListNode;
            if (parentNode == null || e.Item == null)
                return;
            DispatcherHelper.Invoke(Dispatcher, () =>
            {
                if (parentNode.IsExpandButtonVisible == DevExpress.Utils.DefaultBoolean.False)
                    parentNode.IsExpandButtonVisible = DevExpress.Utils.DefaultBoolean.True;
                if (parentNode.Tag == null)
                    return;
                var nodeToAdd = CreateNodeForItem(e.Item);
                TreeList.BeginDataUpdate();
                parentNode.Nodes.Add(nodeToAdd);
                TreeList.EndDataUpdate();
            });
        }
        private void ehFileSystemItemDeleted(object sender, FileSystemItemChangedEventArgs e)
        {
            var parentNode = e?.Parent?.VisualObject as TreeListNode;
            var nodeToDelete = e?.Item?.VisualObject as TreeListNode;
            if (parentNode == null || nodeToDelete == null)
                return;
            DispatcherHelper.Invoke(Dispatcher, () =>
             {
                 if (parentNode.IsExpandButtonVisible == DevExpress.Utils.DefaultBoolean.True && !e.Parent.HasChildren)
                     parentNode.IsExpandButtonVisible = DevExpress.Utils.DefaultBoolean.False;
                 if (parentNode.Tag == null)
                     return;
                 TreeList.BeginDataUpdate();
                 parentNode.Nodes.Remove(nodeToDelete);
                 TreeList.EndDataUpdate();
             });
        }
        private void ehFileSystemItemProcessed(object sender, FileSystemItemProcessedEventArgs e)
        {
            var node = e?.Item?.VisualObject as TreeListNode;
            if (node == null)
                return;
            DispatcherHelper.Invoke(Dispatcher, () =>
            {
                node.IsExpandButtonVisible = e.Item.HasChildren ? DevExpress.Utils.DefaultBoolean.True : DevExpress.Utils.DefaultBoolean.False;
            });

        }
        private void ehFolderSizeCalculated(object sender, FileSystemItemProcessedEventArgs e)
        {
            var folderItem = sender as FolderItem;
            if (folderItem == null)
                return;
            if (folderItem.VisualObject == null)
                return;
            var treeNode = folderItem.VisualObject as TreeListNode;
            if (treeNode == null)
                return;
            List<TreeListNode> temp = new List<TreeListNode>(treeNode.Nodes.Count);
            foreach (TreeListNode treeListNode in treeNode.Nodes)
            {
                temp.Add(treeListNode);
            }
            DispatcherHelper.Invoke(Dispatcher, () =>
            {
                TreeList.BeginDataUpdate();
                treeNode.Nodes.Clear();
                foreach (TreeListNode treeListNode in temp)
                    treeNode.Nodes.Add(treeListNode);
                TreeList.EndDataUpdate();
            });
        }
        void ehLoaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (!SkipDialogOnLoading)
#endif
                ScanFolder();
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
            ScanFolder(dialog.SelectedPath);
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
        private void TreeView_NodeExpanding(object sender, DevExpress.Xpf.Grid.TreeList.TreeListNodeAllowEventArgs e)
        {
            var node = e.Node;
            if (node.Tag != null)
                return;
            var folderItem = e.Node.Content as FolderItem;
            if (folderItem == null)
                return;
            if (!folderItem.HasChildren)
                node.IsExpandButtonVisible = DevExpress.Utils.DefaultBoolean.False;
            TreeList.BeginDataUpdate();
            foreach (FileSystemItem fileSystemItem in folderItem.Children)
            {
                var newNode = CreateNodeForItem(fileSystemItem);
                node.Nodes.Add(newNode);
            }
            node.Tag = true;
            TreeList.EndDataUpdate();
        }

#if DEBUG
        public bool SkipDialogOnLoading { get; set; }
#endif

    }
}
