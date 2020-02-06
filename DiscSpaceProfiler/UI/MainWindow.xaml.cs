using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.Code.FileSystem;
using DiscSpaceProfiler.UI.Interop;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DiscSpaceProfiler.UI
{
    using Timer = System.Timers.Timer;

    public enum NodeUpdateKind
    {
        Add,
        Remove,
        UpdateOrder,
        UpdateExpand,
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ConcurrentQueue<NodeChange> changes;
        bool lastProcessingComplete = true;
        Timer updateTimer;

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
            updateTimer = new Timer();
            updateTimer.Interval = 1000;
            updateTimer.Elapsed += this.UpdateTimer_Elapsed;
            changes = new ConcurrentQueue<NodeChange>();
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
            StopChangesHandling();
            TreeList.View.Nodes.Clear();
            ViewModel.Run(path);
            TreeList.View.Nodes.Add(CreateNodeForItem(ViewModel.RootNode));
            StartChangesHandling();
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
            changes.Enqueue(new NodeChange() { ParentNode = parentNode, ChangedNode = CreateNodeForItem(e.Item), UpdateKind = NodeUpdateKind.Add });
        }
        private void ehFileSystemItemDeleted(object sender, FileSystemItemChangedEventArgs e)
        {
            var parentNode = e?.Parent?.VisualObject as TreeListNode;
            var nodeToDelete = e?.Item?.VisualObject as TreeListNode;
            if (parentNode == null || nodeToDelete == null)
                return;
            changes.Enqueue(new NodeChange() { ParentNode = parentNode, ChangedNode = nodeToDelete, UpdateKind = NodeUpdateKind.Remove });
        }
        private void ehFileSystemItemProcessed(object sender, FileSystemItemProcessedEventArgs e)
        {
            var node = e?.Item?.VisualObject as TreeListNode;
            if (node == null)
                return;
            changes.Enqueue(new NodeChange() { ParentNode = null, ChangedNode = node, UpdateKind = NodeUpdateKind.UpdateExpand });
        }
        private void ehFolderSizeCalculated(object sender, FileSystemItemProcessedEventArgs e)
        {
            var folderItem = e.Item as FolderItem;
            if (folderItem == null)
                return;
            if (folderItem.VisualObject == null)
                return;
            var treeNode = folderItem.VisualObject as TreeListNode;
            if (treeNode == null)
                return;
            changes.Enqueue(new NodeChange() { ParentNode = null, ChangedNode = treeNode, UpdateKind = NodeUpdateKind.UpdateOrder });
        }
        void ehLoaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (!SkipDialogOnLoading)
#endif
                ScanFolder();
        }
        void HandleChanges()
        {
            lock (this)
            {
                if (!lastProcessingComplete)
                    return;
                lastProcessingComplete = false;
            }
            List<NodeChange> changesContainer = new List<NodeChange>();
            while (changes.TryDequeue(out var change))
            {
                changesContainer.Add(change);
                if (changesContainer.Count > 1000)
                    break;
            }
            if (changesContainer.Count == 0)
            {
                lock (this)
                {
                    lastProcessingComplete = true;
                }
                return;
            }
            DispatcherHelper.Invoke(Dispatcher, () => HandleChanges(changesContainer));
        }
        void HandleChanges(List<NodeChange> changesContainer)
        {
            List<TreeListNode> nodesToUpdateOrderImplicit = new List<TreeListNode>();
            List<TreeListNode> nodesToUpdateOrderExplicit = new List<TreeListNode>();
            TreeList.BeginDataUpdate();
            try
            {
                foreach (NodeChange nodeChange in changesContainer)
                {
                    if (nodeChange.UpdateKind == NodeUpdateKind.UpdateOrder)
                    {
                        if (!nodesToUpdateOrderExplicit.Contains(nodeChange.ChangedNode))
                            nodesToUpdateOrderExplicit.Add(nodeChange.ChangedNode);
                    }
                    else if (nodeChange.UpdateKind != NodeUpdateKind.UpdateExpand)
                    {
                        if (!nodesToUpdateOrderImplicit.Contains(nodeChange.ParentNode))
                            nodesToUpdateOrderImplicit.Add(nodeChange.ParentNode);
                    }
                    TreeListNode parentNode = nodeChange.ParentNode;
                    TreeListNode changedNode = nodeChange.ChangedNode;
                    if (changedNode != null && nodeChange.UpdateKind == NodeUpdateKind.UpdateExpand)
                    {
                        var folderItem = changedNode.Content as FolderItem;
                        if (folderItem != null)
                            changedNode.IsExpandButtonVisible = folderItem.HasChildren ? DevExpress.Utils.DefaultBoolean.True : DevExpress.Utils.DefaultBoolean.False;
                    }
                    if (parentNode == null || changedNode == null)
                        continue;
                    if (nodeChange.UpdateKind == NodeUpdateKind.Add)
                    {
                        if (parentNode.IsExpandButtonVisible == DevExpress.Utils.DefaultBoolean.False)
                            parentNode.IsExpandButtonVisible = DevExpress.Utils.DefaultBoolean.True;
                        if (parentNode.Tag == null)
                            continue;
                        parentNode.Nodes.Add(changedNode);
                    }

                    if (nodeChange.UpdateKind == NodeUpdateKind.Remove)
                    {
                        var folderItem = parentNode.Content as FolderItem;
                        if (folderItem != null)
                            if (parentNode.IsExpandButtonVisible == DevExpress.Utils.DefaultBoolean.True && !folderItem.HasChildren)
                                parentNode.IsExpandButtonVisible = DevExpress.Utils.DefaultBoolean.False;
                        if (parentNode.Tag == null)
                            continue;
                        if (parentNode.Nodes.Contains(changedNode))
                            parentNode.Nodes.Remove(changedNode);
                    }
                }
                foreach (TreeListNode treeListNode in nodesToUpdateOrderImplicit)
                {
                    if (nodesToUpdateOrderExplicit.Contains(treeListNode))
                        nodesToUpdateOrderExplicit.Remove(treeListNode);
                }
                foreach (TreeListNode treeListNode in nodesToUpdateOrderExplicit)
                {
                    UpdateFoldersOrder(treeListNode);
                }
            }
            catch
            { }
            finally
            {
                TreeList.EndDataUpdate();
                lock (this)
                {
                    lastProcessingComplete = true;
                }
            }
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
        private void StartChangesHandling()
        {
            lock (this)
                lastProcessingComplete = true;
            updateTimer.Start();
        }
        private void StopChangesHandling()
        {
            updateTimer.Stop();
            lock (this)
                lastProcessingComplete = false;
            while (changes.TryDequeue(out _))
            {

            }
        }
        void StopProfiling()
        {
            ViewModel?.StopProcessing();
            StopChangesHandling();
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
        void UpdateFoldersOrder(TreeListNode treeListNode)
        {

            TreeListNode firstFolder = null;
            foreach (TreeListNode nestedNode in treeListNode.Nodes)
            {
                var folderItem = nestedNode.Content as FolderItem;
                if (folderItem != null)
                {
                    firstFolder = nestedNode;
                    break;
                }
            }
            if (firstFolder != null)
            {
                treeListNode.Nodes.Remove(firstFolder);
                treeListNode.Nodes.Add(firstFolder);
            }
        }
        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            HandleChanges();
        }

#if DEBUG
        public bool SkipDialogOnLoading { get; set; }
#endif

    }
    public class NodeChange
    {
        public TreeListNode ChangedNode { get; set; }
        public TreeListNode ParentNode { get; set; }
        public NodeUpdateKind UpdateKind { get; set; }
    }
}
