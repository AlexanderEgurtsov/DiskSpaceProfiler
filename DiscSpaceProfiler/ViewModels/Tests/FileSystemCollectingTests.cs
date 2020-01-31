#if DEBUG
using NUnit.Framework;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DiscSpaceProfiler.ViewModels.Tests
{
    using FileSystemLocationInfo = Tuple<string, long>;
    [TestFixture]
    public class FileSystemCollectingTests
    {
        [Test]
        public void TestCollectionsOnMockData() 
        {
            var drive = @"C:\\";
            var folder1Path = @"C:\\1";
            var folder2Path = @"C:\\2";
            var folder3Path = @"C:\\3";
            var file1Path = @"C:\\1\1.tmp";
            var file2Path = @"C:\\2\2.tmp";
            var mockDataProvider = new Mock<IFileSystemDataProvider>();
            var mockFileSystemWatcher = new Mock<IFileSystemWatcher>();
            mockDataProvider.Setup(dp => dp.GetDrives()).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(drive, 3)});
            mockDataProvider.Setup(dp => dp.GetDirectories(It.Is<string>(path => path == drive))).Returns(new string[] { folder1Path, folder2Path, folder3Path });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder1Path))).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(file1Path, 1) });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder2Path))).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(file2Path, 2) });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder3Path))).Returns(new FileSystemLocationInfo[] {  });
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == drive))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder1Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder2Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder3Path))).Returns(true);
            MainWindowViewModel model = new MainWindowViewModel(mockDataProvider.Object, mockFileSystemWatcher.Object, 1, 1);
            var scanCompleted = false;
            model.ScanCompleted += (s, ea)=> { scanCompleted = true; };
            model.Run();
            while (!scanCompleted)
            {
                
            }
            Assert.IsNotNull(model.RootNodes);
            Assert.AreEqual(1, model.RootNodes.Count());
            var driveItem = model.RootNodes.First();
            Assert.IsNotNull(driveItem);
            Assert.AreEqual(3, driveItem.Size);
            Assert.IsTrue(driveItem.IsValid);
            Assert.AreEqual(3, driveItem.Children.Count());
            var folders = driveItem.Children.ToArray();
            var folder1Item = folders[0];
            Assert.IsNotNull(folder1Item);
            Assert.AreEqual(1, folder1Item.Size);
            Assert.IsTrue(folder1Item.IsValid);
            Assert.AreEqual(1, folder1Item.Children.Count());
            Assert.AreEqual(file1Path, folder1Item.Children.First().Path);
            Assert.AreEqual(1, folder1Item.Children.First().Size);

            var folder2Item = folders[1];
            Assert.IsNotNull(folder2Item);
            Assert.AreEqual(2, folder2Item.Size);
            Assert.IsTrue(folder2Item.IsValid);
            Assert.AreEqual(1, folder1Item.Children.Count());
            Assert.AreEqual(file2Path, folder2Item.Children.First().Path);
            Assert.AreEqual(2, folder2Item.Children.First().Size);

            var folder3Item = folders[2];
            Assert.IsNotNull(folder3Item);
            Assert.AreEqual(0, folder3Item.Size);
            Assert.IsTrue(folder3Item.IsValid);
            Assert.AreEqual(0, folder3Item.Children.Count());
        }

        void CreateFiles(string newPath, int maxItemsCount)
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
        void GenerateFolder(string rootPath, string prefix, int maxItemsCount) 
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

        void CheckFiles(FileSystemItem fileSystemItem, string filePath)
        {
            Assert.IsTrue(fileSystemItem is FileItem);
            Assert.AreEqual(filePath, fileSystemItem.Path);
            Assert.AreEqual(Path.GetFileName(filePath), fileSystemItem.DisplayName);
            Assert.AreEqual(1, fileSystemItem.Size);
            
        }
        void CheckFolders(FileSystemItem rootNode,string rootPath)
        {
            Assert.IsTrue(rootNode is FolderItem);
            Assert.AreEqual(rootPath, rootNode.Path);
            Assert.AreEqual(Path.GetFileName(rootPath), rootNode.DisplayName);
            Assert.IsTrue(rootNode.IsValid);
            var directories = Directory.GetDirectories(rootPath);
            var files = Directory.GetFiles(rootPath);
            var childrens = rootNode.Children.ToList();
            Assert.AreEqual(childrens.Count, files.Length + directories.Length);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var childFile = rootNode.FindChildren(file, Path.GetFileName(file));
                Assert.IsNotNull(childFile);
                CheckFiles(childFile, file);
            }
            for (int i = 0; i < directories.Length; i++)
            {
                var folder = directories[i];
                var childFolder = rootNode.FindChildren(folder, Path.GetFileName(folder));
                Assert.IsNotNull(childFolder);
                CheckFolders(childFolder, folder);
            }
            
        }

        void SetupTest(string rootPath, int maxCount, bool removeRoot = true)
        {
            if (removeRoot && Directory.Exists(rootPath))
                Delete(rootPath);
            GenerateFolder(rootPath, "", maxCount);
        }


        void Delete(string rootPath)
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
            SetupTest(rootPath, 3);
            try
            {
                var model = new MainWindowViewModel();
                var scanCompleted = false;
                model.ScanCompleted += (s, ea) => { scanCompleted = true; };
                model.Run(rootPath);
                while (!scanCompleted)
                {

                }
                CheckFolders(model.RootNodes.First(), rootPath);
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
                System.Threading.Thread.Sleep(300);
                SetupTest(rootPath, 3, false);
                System.Threading.Thread.Sleep(300);

                while (model.HasChanges || model.HasTasksToScan)
                {
                    System.Threading.Thread.Sleep(300);
                }
                
                CheckFolders(model.RootNodes.First(), rootPath);
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
                    SetupTest(rootPath, 5, false);
                    dataIsCreated = true;
                });
                
                model.Run(rootPath);
                System.Threading.Thread.Sleep(300);
                while (model.HasChanges || !dataIsCreated || model.HasTasksToScan)
                {
                    System.Threading.Thread.Sleep(300);
                }
                
                CheckFolders(model.RootNodes.First(), rootPath);
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
                var scanCompleted = false;
                model.ScanCompleted += (s, ea) => { scanCompleted = true; };
                model.Run(rootPath);
                while (!scanCompleted)
                {

                }
                scanCompleted = false;
                Directory.Move(Path.Combine(rootPath, "Folder0"), Path.Combine(rootPath, "Folder1", "FolderNew"));
                System.Threading.Thread.Sleep(300);
                while (model.HasChanges && !scanCompleted)
                {
                    System.Threading.Thread.Sleep(300);
                }
                CheckFolders(model.RootNodes.First(), rootPath);
            }
            finally
            {
                Delete(rootPath);
            }
        }
        [Test]
        public void TestUpdateOnMockData()
        {
            var drive = @"C:\";
            var folder1Path = @"C:\1";
            var folder2Path = @"C:\2";
            var folder3Path = @"C:\3";
            var file1Path = @"C:\1\1.tmp";
            var file2Path = @"C:\2\2.tmp";
            var mockDataProvider = new Mock<IFileSystemDataProvider>();
            mockDataProvider.Setup(dp => dp.GetDrives()).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(drive, 3) });
            mockDataProvider.Setup(dp => dp.GetDirectories(It.Is<string>(path => path == drive))).Returns(new string[] { folder1Path, folder2Path, folder3Path });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder1Path))).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(file1Path, 1) });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder2Path))).Returns(new FileSystemLocationInfo[] { new FileSystemLocationInfo(file2Path, 2) });
            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == folder3Path))).Returns(new FileSystemLocationInfo[] { });
            
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == drive))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder1Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder2Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == folder3Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.DirectoryExists(It.Is<string>(path => path == @"C:\4"))).Returns(true);
            mockDataProvider.Setup(dp => dp.FileExists(It.Is<string>(path => path == file1Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.FileExists(It.Is<string>(path => path == file2Path))).Returns(true);
            mockDataProvider.Setup(dp => dp.GetFileInfo(It.Is<string>(path => path == file2Path))).Returns( new FileSystemLocationInfo(file2Path, 5));
            var mockFileSystemWatcher = new Mock<IFileSystemWatcher>();
            
            
            MainWindowViewModel model = new MainWindowViewModel(mockDataProvider.Object, mockFileSystemWatcher.Object, 1, 1);
            var scanCompleted = false;
            model.ScanCompleted += (s, ea) => { scanCompleted = true; };
            model.Run();
            while (!scanCompleted)
            {

            }
            var driveItem = model.RootNodes.First();
            Assert.IsNotNull(model.FindItem(folder1Path));
            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("1", folder1Path, FileSystemChangeType.Deletion))
                ;
            System.Threading.Thread.Sleep(100);
            Assert.AreEqual(2, driveItem.Children.Count());
            Assert.AreEqual(2, driveItem.Size);
            Assert.IsNull(model.FindItem(folder1Path));

            Assert.IsNull(model.FindItem(@"C:\4"));

            mockDataProvider.Setup(dp => dp.GetFiles(It.Is<string>(path => path == @"C:\4"))).Returns(new FileSystemLocationInfo[] { });
            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("4", @"C:\4", FileSystemChangeType.Creation));
                ;
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(3, driveItem.Children.Count());
            Assert.IsNotNull(model.FindItem(@"C:\4"));

            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("2.tmp",file2Path, FileSystemChangeType.Change));
            ;
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(5, driveItem.Size);
            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("2", folder2Path, FileSystemChangeType.Deletion))
            ;
            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("3", folder3Path, FileSystemChangeType.Deletion))
            ;
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(1, driveItem.Children.Count());
            Assert.AreEqual(0, driveItem.Size);
            var folder4 = driveItem.Children.FirstOrDefault();
            Assert.IsNotNull(folder4);
            Assert.IsNull(model.FindItem(@"C:\41"));
            mockFileSystemWatcher.Raise(
                fsw => fsw.Changed += null, mockFileSystemWatcher.Object, new FileSystemChangeEventArgs("41", @"C:\41", "4", @"C:\4"));
            ;
            System.Threading.Thread.Sleep(100);
            Assert.IsNotNull(model.FindItem(@"C:\41"));
            Assert.AreEqual("41", folder4.DisplayName);
            Assert.AreEqual(@"C:\41", folder4.Path);
        }
    }
}
#endif
