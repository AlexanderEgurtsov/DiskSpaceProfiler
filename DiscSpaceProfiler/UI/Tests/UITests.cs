#if DEBUG
using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.Code.FileSystem;
using DiscSpaceProfiler.Code.FileSystem.Tests;
using DiscSpaceProfiler.ViewModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscSpaceProfiler.UI.Tests
{
    [TestFixture, RequiresThread(ApartmentState.STA)]
    public class UITests
    {
        void CheckFile(FileSystemItem fileSystemItem, string filePath, out long fileSize)
        {
            Assert.IsTrue(fileSystemItem is FileItem);
            Assert.AreEqual(Path.GetFileName(filePath), fileSystemItem.DisplayName);
            Assert.AreEqual(filePath, fileSystemItem.GetPath());
            FileInfo info = new FileInfo(filePath);
            fileSize = info.Length;
            Assert.AreEqual(fileSize, fileSystemItem.Size, filePath);

        }
        void CheckFolders(TreeListNode rootTreeListNode, string rootPath, MainWindowViewModel model, out long folderSize)
        {
            folderSize = 0;
            var rootNode = rootTreeListNode.Content as FolderItem;
            
            Assert.IsTrue(rootNode is FolderItem);
            Assert.AreEqual(rootPath, (rootNode as FolderItem).GetPath());
            if (rootNode.Parent != null)
                Assert.AreEqual(Path.GetFileName(rootPath), rootNode.DisplayName);
            else
                Assert.AreEqual(rootPath, rootNode.DisplayName);
            Assert.IsTrue(rootNode.IsValid);
            var directories = Directory.GetDirectories(rootPath);
            var files = Directory.GetFiles(rootPath);
            var childrens = rootNode.Children.ToList();
            Assert.AreEqual(childrens.Count, files.Length + directories.Length);
            Assert.AreEqual(rootTreeListNode.Nodes.Count, childrens.Count);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var childFile = (rootNode as FolderItem)?.FindChildren(Path.GetFileName(file));
                Assert.IsNotNull(childFile);
                CheckFile(childFile, file, out var fileSize);
                folderSize += fileSize;
            }
            
            for (int i = 0; i < directories.Length; i++)
            {
                var folder = directories[i];
                var childFolder = (rootNode as FolderItem)?.FindChildren(Path.GetFileName(folder));
                Assert.IsNotNull(childFolder);
                Assert.IsNotNull(model.FindItem(folder));
                var childTreeNode = FindTreeNodeByFolder(rootTreeListNode, childFolder as FolderItem);
                Assert.IsNotNull(childTreeNode);
                Assert.IsNotNull(childTreeNode.Content);
                Assert.AreEqual(childFolder, childTreeNode.Content);
                CheckFolders(childTreeNode, folder, model, out var nestedFolderSize);
                folderSize += nestedFolderSize;
            }
            Assert.AreEqual(folderSize, rootNode.Size, rootPath);
        }
        TreeListNode FindTreeNodeByFolder(TreeListNode node, FolderItem folderItem)
        {
            for (int i = 0; i < node.Nodes.Count; i++)
            {
                if (node.Nodes[i].Content == folderItem)
                    return node.Nodes[i];
            }
            return null;
        }
        [Test, RequiresThread(ApartmentState.STA)]
        public void TestCollecting()
        {
            MainWindow window = new MainWindow();
            window.SkipDialogOnLoading = true;
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestCollecting));
            FileSystemCollectingTests.Delete(rootPath);
            Directory.CreateDirectory(rootPath);
            try
            {
                var model = window.ViewModel;
                window.Show();
                FileSystemCollectingTests.SetupTest(rootPath, 3, false);
                window.ScanFolder(rootPath);
                window.TreeView.ExpandAllNodes();
                while (model.HasChanges || model.IsScanning)
                {

                }
                Assert.IsNotNull(window.TreeView.Nodes);
                Assert.AreEqual(1, window.TreeView.Nodes.Count);
                var rootNode = window.TreeView.Nodes[0];
                Assert.IsNotNull(rootNode);
                window.TreeView.ExpandAllNodes();
                CheckFolders(rootNode, rootPath, model, out _);
            }
            finally
            {
                FileSystemCollectingTests.Delete(rootPath);
            }
        }
    }
}
#endif
