﻿#if DEBUG
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using DiscSpaceProfiler.ViewModels;

namespace DiscSpaceProfiler.Code.FileSystem.Tests
{
    [TestFixture]
    public class FileSystemCollectingTests
    {
        public static void CreateFiles(string newPath, int maxItemsCount)
        {
            for (int i = 0; i < maxItemsCount; i++)
            {
                using (var stream = File.Create(Path.Combine(newPath, $"File{i}.txt")))
                {
                    stream.Write(new byte[] { 134 }, 0, 1);
                    stream.Flush();
                    stream.Close();
                }
                
            }
        }
        public static void GenerateFolder(string rootPath, string prefix, int maxItemsCount) 
        {
            if (maxItemsCount <= 0)
                return;
            for (int i = 0; i < maxItemsCount; i++)
            {
                var newPath = Path.Combine(rootPath, $"Folder{prefix}{i}");
                Directory.CreateDirectory(newPath);
                CreateFiles(newPath, maxItemsCount);
                GenerateFolder(newPath, prefix + i, maxItemsCount - 1);
            }
        }

        void CheckFile(FileSystemItem fileSystemItem, string filePath, out long fileSize)
        {
            Assert.IsTrue(fileSystemItem is FileItem);
            Assert.AreEqual(Path.GetFileName(filePath), fileSystemItem.DisplayName);
            Assert.AreEqual(filePath, fileSystemItem.GetPath());
            FileInfo info = new FileInfo(filePath);
            fileSize = info.Length;
            Assert.AreEqual(fileSize, fileSystemItem.Size, filePath);
            
        }
        void CheckFolders(FileSystemItem rootNode, string rootPath, MainWindowViewModel model, out long folderSize)
        {
            folderSize = 0;
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
                CheckFolders(childFolder, folder, model, out var nestedFolderSize);
                folderSize += nestedFolderSize;
            }
            Assert.AreEqual(folderSize, rootNode.Size, rootPath);
        }

        public static void SetupTest(string rootPath, int maxCount, bool removeRoot = true)
        {
            if (removeRoot && Directory.Exists(rootPath))
                Delete(rootPath);
            GenerateFolder(rootPath, "", maxCount);
        }
        public static void Delete(string rootPath)
        {
            try
            {
                if (!Directory.Exists(rootPath))
                    return;
                var directories = Directory.GetDirectories(rootPath);
                foreach (string directory in directories)
                {
                    Delete(directory);
                }
                var files = Directory.GetFiles(rootPath);
                foreach (string file in files)
                {
                    File.Delete(file);
                }
                Directory.Delete(rootPath);
            }
            catch
            {
                
            }
        }
        [Test]
        public void TestCollectingOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestCollectingOnRealData));
            SetupTest(rootPath, 5);
            try
            {
                var model = new MainWindowViewModel();
                Assert.AreEqual(string.Empty, model.RootPath);
                Assert.IsFalse(model.ProcessingIsActive);
                model.Run(rootPath);
                Assert.IsTrue(model.ProcessingIsActive);
                while (model.IsScanning)
                {

                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                Assert.AreEqual(rootPath, model.RootPath);
                model.StopProcessing();
                Assert.IsFalse(model.ProcessingIsActive);
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestWatchingOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestWatchingOnRealData));
            Delete(rootPath);
            Directory.CreateDirectory(rootPath);
            try
            {
                var model = new MainWindowViewModel();
                model.Run(rootPath);
                SetupTest(rootPath, 3, false);
                while (model.HasChanges || model.IsScanning)
                {
                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestCollectingAndWatchingOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestCollectingAndWatchingOnRealData));

            Delete(rootPath);

            Directory.CreateDirectory(rootPath);

            try
            {
                var model = new MainWindowViewModel();
                bool dataIsCreated = false;
                Task.Run(() =>
                {
                    SetupTest(rootPath, 4, false);
                    dataIsCreated = true;
                });
                
                model.Run(rootPath);
                while (model.HasChanges || !dataIsCreated || model.IsScanning)
                {
                    
                }
                
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestMoveDirectoryOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestMoveDirectoryOnRealData));
            SetupTest(rootPath, 2);
            try
            {
                var model = new MainWindowViewModel();
                model.Run(rootPath);
                while (model.IsScanning)
                {

                }
                Directory.Move(Path.Combine(rootPath, "Folder0"), Path.Combine(rootPath, "Folder1", "FolderNew"));
                System.Threading.Thread.Sleep(300);
                while (model.HasChanges || model.IsScanning)
                {
                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestRenameOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestRenameOnRealData));
            SetupTest(rootPath, 2);
            try
            {
                var model = new MainWindowViewModel();
                model.Run(rootPath);
                while (model.HasChanges || model.IsScanning)
                {

                }
                File.Move(Path.Combine(rootPath, @"Folder0\File0.txt"), Path.Combine(rootPath, @"Folder0\NewFile0.txt"));
                System.Threading.Thread.Sleep(300);
                while (model.HasChanges || model.IsScanning)
                {
                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestFileChangeOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestFileChangeOnRealData));
            SetupTest(rootPath, 2);
            try
            {
                var model = new MainWindowViewModel();
                model.Run(rootPath);
                using (var stream = File.Create(Path.Combine(rootPath, @"Folder0\File0.txt")))
                {
                    stream.Write(new byte[] { 134, 135 }, 0, 2);
                    stream.Flush();
                    stream.Close();
                }
                System.Threading.Thread.Sleep(300);
                while (model.HasChanges || model.IsScanning)
                {
                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestCollectingAndWatchingOnRealDataWithDelete()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(TestCollectingAndWatchingOnRealDataWithDelete));

            Delete(rootPath);

            Directory.CreateDirectory(rootPath);

            try
            {
                var model = new MainWindowViewModel();
                bool dataIsCreated = false;
                Task.Run(() =>
                {
                    SetupTest(rootPath, 4, false);
                    dataIsCreated = true;
                });

                model.Run(rootPath);
                
                while (model.HasChanges || !dataIsCreated || model.IsScanning)
                {
                }

                CheckFolders(model.RootNode, rootPath, model, out _);
                foreach (string item in Directory.GetDirectories(rootPath))
                {
                    Delete(item);
                }
                while (model.HasChanges || !dataIsCreated || model.IsScanning)
                {
                }
                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        [Explicit]
        public void StressTestCollectingAndWatchingOnRealData()
        {
            var tempPath = Path.GetTempPath();
            var rootPath = Path.Combine(tempPath, nameof(StressTestCollectingAndWatchingOnRealData));

            Delete(rootPath);

            Directory.CreateDirectory(rootPath);

            try
            {
                var model = new MainWindowViewModel();
                bool dataIsCreated = false;
                Task.Run(() =>
                {
                    SetupTest(rootPath, 7, false);
                    dataIsCreated = true;
                });

                model.Run(rootPath);
                while (model.HasChanges || !dataIsCreated || model.IsScanning)
                {

                }

                CheckFolders(model.RootNode, rootPath, model, out _);
                model.StopProcessing();
            }
            finally
            {
                Delete(rootPath);
            }
        }
    }
}
#endif
