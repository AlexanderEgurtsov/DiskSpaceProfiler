#if DEBUG
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscSpaceProfiler.ViewModels.Tests
{
    [TestFixture]
    public class FileSystemItemTests
    {
        [Test]
        public void DrivePropertiesTest() {
            var driveItem = new DriveItem(@"C:\\");
            Assert.AreEqual(@"C:\\", driveItem.DisplayName);
            Assert.AreEqual(@"C:\\", driveItem.Path);
            Assert.IsNull(driveItem.Parent);
            Assert.IsFalse(driveItem.HasChildren);
            Assert.IsFalse(driveItem.IsFile);
            Assert.AreEqual(0, driveItem.Size);
        }
        [Test]
        public void FilePropertiesTest()
        {
            var fileItem = new FileItem(@"C:\\1.txt", "1.txt", 10);
            Assert.AreEqual(@"1.txt", fileItem.DisplayName);
            Assert.AreEqual(@"C:\\1.txt", fileItem.Path);
            Assert.IsNull(fileItem.Parent);
            Assert.IsFalse(fileItem.HasChildren);
            Assert.IsTrue(fileItem.IsFile);
            Assert.IsNotNull(fileItem.Children);
            Assert.AreEqual(0, fileItem.Children.Count());
            Assert.AreEqual(10, fileItem.Size);
        }
        [Test]
        public void FolderPropertiesTest()
        {
            var folderItem = new FolderItem(@"C:\\1", "1");
            Assert.AreEqual(@"1", folderItem.DisplayName);
            Assert.AreEqual(@"C:\\1", folderItem.Path);
            Assert.IsNull(folderItem.Parent);
            Assert.IsFalse(folderItem.HasChildren);
            Assert.IsFalse(folderItem.IsFile);
            Assert.AreEqual(0, folderItem.Size);
        }
        [Test]
        public void DriveFolderStructureTest() 
        {
            var drive = new DriveItem(@"C:\\");
            var folder1 = new FolderItem(@"C:\\1", "1");
            var folder2 = new FolderItem(@"C:\\2", "2");
            drive.AddChildren(folder1);
            drive.AddChildren(folder2);
            Assert.AreEqual(drive, folder1.Parent);
            Assert.AreEqual(drive, folder2.Parent);
            Assert.IsFalse(drive.IsValid);
            Assert.IsFalse(folder1.IsValid);
            Assert.IsFalse(folder2.IsValid);
            Assert.AreEqual(2, drive.Children.Count());
            Assert.IsTrue(drive.Children.Contains(folder1));
            Assert.IsTrue(drive.Children.Contains(folder2));
            Assert.AreEqual(0, drive.Size);
            Assert.AreEqual(0, folder1.Size);
            Assert.AreEqual(0, folder2.Size);
        }
        [Test]
        public void DriveFolderFileStructureTest()
        {
            var drive = new DriveItem(@"C:\\");
            var folder1 = new FolderItem(@"C:\\1", "1");
            var folder2 = new FolderItem(@"C:\\2", "2");
            drive.AddChildren(folder1);
            drive.AddChildren(folder2);
            Assert.AreEqual(drive, folder1.Parent);
            Assert.AreEqual(drive, folder2.Parent);
            Assert.IsFalse(drive.IsValid);
            Assert.IsFalse(folder1.IsValid);
            Assert.IsFalse(folder2.IsValid);
            Assert.AreEqual(2, drive.Children.Count());
            Assert.IsTrue(drive.Children.Contains(folder1));
            Assert.IsTrue(drive.Children.Contains(folder2));
            Assert.AreEqual(0, drive.Size);
            Assert.AreEqual(0, folder1.Size);
            Assert.AreEqual(0, folder2.Size);
            var file1_1 = new FileItem(@"C:\\1\\1.txt", "1.txt", 1);
            var file2_1 = new FileItem(@"C:\\2\\1.txt", "1.txt", 2);
            folder1.AddChildren(file1_1);
            Assert.AreEqual(folder1, file1_1.Parent);
            Assert.AreEqual(1, folder1.Size);
            Assert.IsTrue(folder1.IsValid);
            Assert.IsFalse(drive.IsValid);
            Assert.AreEqual(1, drive.Size);
            folder2.AddChildren(file2_1);
            Assert.AreEqual(folder2, file2_1.Parent);
            Assert.AreEqual(2, folder2.Size);
            Assert.IsTrue(folder2.IsValid);
            Assert.IsTrue(drive.IsValid);
            Assert.AreEqual(3, drive.Size);
        }
        [Test]
        public void NestedFolderFileStructureTest()
        {
            var folder1 = new FolderItem(@"C:\1", "1");
            var folder2 = new FolderItem(@"C:\1\2", "2");
            Assert.IsFalse(folder1.IsValid);
            Assert.IsFalse(folder2.IsValid);
            Assert.AreEqual(0, folder1.Size);
            Assert.AreEqual(0, folder2.Size);
            var file1_1 = new FileItem(@"C:\1\1.txt", "1.txt", 1);
            var file2_1 = new FileItem(@"C:\1\2\1.txt", "1.txt", 2);
            folder1.AddChildren(folder2);
            folder1.AddChildren(file1_1);
            Assert.AreEqual(2, folder1.Children.Count());
            Assert.AreEqual(folder1, file1_1.Parent);
            Assert.AreEqual(1, folder1.Size);
            Assert.IsFalse(folder1.IsValid);
            folder2.AddChildren(file2_1);
            Assert.AreEqual(folder2, file2_1.Parent);
            Assert.AreEqual(2, folder2.Size);
            Assert.IsTrue(folder2.IsValid);
            Assert.IsTrue(folder1.IsValid);
            Assert.AreEqual(3, folder1.Size);
        }

        [Test]
        public void NestedFolderFindRemoveStructureTest()
        {
            var folder1 = new FolderItem(@"C:\1", "1");
            var folder2 = new FolderItem(@"C:\1\1", "1");
            Assert.IsFalse(folder1.IsValid);
            Assert.IsFalse(folder2.IsValid);
            Assert.AreEqual(0, folder1.Size);
            Assert.AreEqual(0, folder2.Size);
            var file1_1 = new FileItem(@"C:\1\1.txt", "1.txt", 1);
            var file2_1 = new FileItem(@"C:\1\1\1.txt", "1.txt", 2);
            folder1.AddChildren(folder2);
            folder1.AddChildren(file1_1);
            Assert.AreEqual(2, folder1.Children.Count());
            Assert.AreEqual(folder1, file1_1.Parent);
            Assert.AreEqual(1, folder1.Size);
            Assert.IsFalse(folder1.IsValid);
            folder2.AddChildren(file2_1);
            Assert.AreEqual(folder2, file2_1.Parent);
            Assert.AreEqual(2, folder2.Size);
            Assert.IsTrue(folder2.IsValid);
            Assert.IsTrue(folder1.IsValid);
            Assert.AreEqual(3, folder1.Size);
            var foundFolder = folder1.FindChildren("1");
            Assert.AreEqual(folder2, foundFolder);
            var foundFile = folder1.FindChildren("1.txt");
            Assert.AreEqual(file1_1, foundFile);
            folder1.RemoveChildren("1");
            Assert.AreEqual(1, folder1.Size);
            folder1.RemoveChildren("1.txt");
            Assert.AreEqual(0, folder1.Size);
        }

        [Test]
        public void NestedFolderRemoveSetSizeStructureTest()
        {
            var folder1 = new FolderItem(@"C:\1", "1");
            var folder2 = new FolderItem(@"C:\1\1", "1");
            Assert.IsFalse(folder1.IsValid);
            Assert.IsFalse(folder2.IsValid);
            Assert.AreEqual(0, folder1.Size);
            Assert.AreEqual(0, folder2.Size);
            var file1_1 = new FileItem(@"C:\1\1.txt", "1.txt", 1);
            var file2_1 = new FileItem(@"C:\1\1\1.txt", "1.txt", 2);
            folder1.AddChildren(folder2);
            folder1.AddChildren(file1_1);
            Assert.AreEqual(2, folder1.Children.Count());
            Assert.AreEqual(folder1, file1_1.Parent);
            Assert.AreEqual(1, folder1.Size);
            Assert.IsFalse(folder1.IsValid);
            folder2.AddChildren(file2_1);
            Assert.AreEqual(folder2, file2_1.Parent);
            Assert.AreEqual(2, folder2.Size);
            Assert.IsTrue(folder2.IsValid);
            Assert.IsTrue(folder1.IsValid);
            Assert.AreEqual(3, folder1.Size);
            folder1.RenameChildren("1.txt", @"C:\1\1.txt", "new.txt", @"C:\1\new.txt");
            var foundFile = folder1.FindChildren("1.txt");
            Assert.IsNull(foundFile);
            foundFile = folder1.FindChildren("new.txt");
            Assert.IsNotNull(foundFile);
            foundFile.SetSize(100);
            Assert.AreEqual(100, foundFile.Size);
            Assert.AreEqual(102, folder1.Size);
        }


    }
}
#endif