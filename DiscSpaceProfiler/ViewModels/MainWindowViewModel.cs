using DevExpress.Xpf.Grid;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using System.Threading.Tasks;
using System.Timers;

namespace DiscSpaceProfiler.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        const int INT_MaxNestingLevel = 2;
        const int INT_MaxTaskCount = 150;

        List<Task> activeTasks = new List<Task>();
        ConcurrentQueue<FileSystemChangeEventArgs> changedEvents = new ConcurrentQueue<FileSystemChangeEventArgs>();
        IFileSystemDataProvider fileSystemDataProvider;
        Dictionary<string, FileSystemItem> fileSystemHash = new Dictionary<string, FileSystemItem>();
        ConcurrentQueue<FileSystemItem> foldersToScan = new ConcurrentQueue<FileSystemItem>();
        int maxNestingLevel;
        int maxTasksCount;
        List<FileSystemItem> rootNodes = new List<FileSystemItem>();
        Timer scanMonitorTimer;
        IFileSystemWatcher watcher;

        public MainWindowViewModel() : this(new DefaultFileSystemDataProvider(), new DefaultFileSystemWatcher(), INT_MaxTaskCount, INT_MaxNestingLevel)
        {

        }

        public MainWindowViewModel(IFileSystemDataProvider fileSystemDataProvider, IFileSystemWatcher watcher, int tasksCount, int maxNestingLevel)
        {
            this.fileSystemDataProvider = fileSystemDataProvider;
            this.maxTasksCount = tasksCount;
            this.maxNestingLevel = maxNestingLevel;
            this.watcher = watcher;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ScanCompleted;

        public IEnumerable<FileSystemItem> RootNodes => rootNodes;

        public void Run(string rootFolder = null)
        {
            if (string.IsNullOrEmpty(rootFolder))
                RunForLocalDrive();
            else
                RunForFolder(rootFolder);
            scanMonitorTimer = new Timer(100);
            scanMonitorTimer.Elapsed += ehScanMonitorTimerElapsed;
            scanMonitorTimer.Start();
        }

        void AddFolderToScan(FileSystemItem item)
        {
            foldersToScan.Enqueue(item);
        }

        void CollectNestedItems(FileSystemItem parentItem, int nestingLevel)
        {
            if (parentItem == null || parentItem.IsFile)
                return;
            var parentPath = parentItem.Path;
            if (string.IsNullOrEmpty(parentPath) || !fileSystemDataProvider.DirectoryExists(parentPath))
                return;
            var directories = fileSystemDataProvider.GetDirectories(parentPath);
            var files = fileSystemDataProvider.GetFiles(parentPath);
            var nestedFolders = new List<FileSystemItem>(directories.Count());
            foreach (string directory in directories)
            {
                FolderItem folderItem = new FolderItem(directory, Path.GetFileName(directory));
                parentItem.AddChildren(folderItem);
                nestedFolders.Add(folderItem);
                UpdateSearchInfo(directory, folderItem);
            }
            foreach (var fileInfo in files)
            {
                FileItem fileItem = new FileItem(fileInfo.Item1, Path.GetFileName(fileInfo.Item1), fileInfo.Item2);
                parentItem.AddChildren(fileItem);
            }
            if (!parentItem.HasChildren)
            {
                (parentItem as FileSystemItemWithChildren)?.UpdateIsValid(true);
                return;
            }

            foreach (var folderItem in nestedFolders)
            {
                if (nestingLevel > maxNestingLevel)
                    AddFolderToScan(folderItem);
                else
                    CollectNestedItems(folderItem, nestingLevel + 1);
            }

        }
        void ehChanged(object sender, FileSystemChangeEventArgs e)
        {
            changedEvents.Enqueue(e);
            Debug.WriteLine($"{e.Name} {e.Path} {e.ChangeType}");
        }

        void ehScanMonitorTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (foldersToScan.Count == 0)
            {
                scanMonitorTimer.Stop();
                OnScanCompleted();
                return;
            }
            if (activeTasks.Count == 0)
            {
                for (int i = 0; i < maxTasksCount; i++)
                {
                    activeTasks.Add(Task.Run(FolderScanTask));
                }
            }
            for (int i = 0; i < activeTasks.Count; i++)
            {
                Task task = activeTasks[i];
                if (task.IsCompleted || task.IsCanceled)
                    activeTasks[i] = Task.Run(FolderScanTask);
            }
        }

        public FileSystemItem FindItem(string parentPath)
        {
            if (fileSystemHash.TryGetValue(parentPath, out var result))
                return result;
            return null;
        }

        void FolderScanTask()
        {
            if (!foldersToScan.TryDequeue(out var currentItem))
                return;
            CollectNestedItems(currentItem, 0);
        }

        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        void OnScanCompleted()
        {
            StartListeningForChanges();
            ScanCompleted?.Invoke(this, EventArgs.Empty);
        }
        void ProcessChange(FileSystemItem parentItem, FileSystemChangeEventArgs change)
        {
            if (change == null)
                return;
            switch (change.ChangeType)
            {
                case FileSystemChangeType.Change:
                    ProcessChange(parentItem, change.Path, change.Name);
                    break;
                case FileSystemChangeType.Deletion:
                    var deletedItem = parentItem.RemoveChildren(change.Path, change.Name);
                    if (deletedItem != null && !deletedItem.IsFile)
                        UpdateSearchInfo(change.Path, null);
                    break;
                case FileSystemChangeType.Creation:
                    ProcessCreation(parentItem, change);
                    break;
                case FileSystemChangeType.Rename:
                    var newItem = parentItem.RenameChildren(change.OldName, change.OldPath, change.Name, change.Path);
                    if (newItem is FolderItem folderItem)
                        UpdateSearchInfo(change.Path, folderItem);
                    break;
            }
        }

        void ProcessChange(FileSystemItem parentItem, string path, string name)
        {
            if (!fileSystemDataProvider.FileExists(path))
                return;
            var fileInfo = fileSystemDataProvider.GetFileInfo(path);
            if (fileInfo == null)
                return;
            var fileItem = parentItem.FindChildren(path, name);
            if (fileItem == null)
                return;
            if (fileItem.Size != fileInfo.Item2)
                fileItem.SetSize(fileInfo.Item2);
        }

        void ProcessChangeForFolder(string parentPath, FileSystemChangeEventArgs change)
        {
            if (string.IsNullOrEmpty(parentPath) || change == null)
                return;
            var parentItem = FindItem(parentPath);
            if (parentItem == null)
                return;
            ProcessChange(parentItem, change);
        }
        void ProcessChangesTask()
        {
            while (true)
            {
                if (changedEvents.TryDequeue(out var change))
                {
                    var parentPath = Path.GetDirectoryName(change.Path);
                    ProcessChangeForFolder(parentPath, change);
                }
            }
        }
        void ProcessCreation(FileSystemItem parentItem, FileSystemChangeEventArgs change)
        {
            var path = change.Path;
            if (fileSystemDataProvider.FileExists(path))
            {
                var fileInfo = fileSystemDataProvider.GetFileInfo(path);
                if (fileInfo == null)
                    return;
                var fileItem = new FileItem(path, change.Name, fileInfo.Item2);
                parentItem.AddChildren(fileItem);
            }
            else if (fileSystemDataProvider.DirectoryExists(path))
            {
                var folderItem = new FolderItem(path, change.Name);
                parentItem.AddChildren(folderItem);
                UpdateSearchInfo(path, folderItem);
                folderItem.UpdateIsValid(true);
            }
        }

        void RunForFolder(string rootFolder)
        {
            rootNodes.Clear();
            var folderItem = new FolderItem(rootFolder, Path.GetFileName(rootFolder));
            rootNodes.Add(folderItem);
            OnPropertyChanged(nameof(RootNodes));
            UpdateSearchInfo(rootFolder, folderItem);
            CollectNestedItems(folderItem, maxNestingLevel);
        }

        void RunForLocalDrive()
        {
            rootNodes.Clear();
            var drives = fileSystemDataProvider.GetDrives();
            if (drives == null)
                return;
            var driveInfo = drives.First();
            DriveItem driveItem = new DriveItem(driveInfo.Item1);
            UpdateSearchInfo(driveItem.Path, driveItem);
            rootNodes.Add(driveItem);
            OnPropertyChanged(nameof(RootNodes));
            CollectNestedItems(driveItem, maxNestingLevel);
        }

        private void StartListeningForChanges()
        {
            if (!watcher.Active)
            {
                watcher.Changed += ehChanged;
                Task.Run(ProcessChangesTask);
                watcher.Start(RootNodes.FirstOrDefault().Path);
            }
        }

        private void UpdateSearchInfo(string directory, FileSystemItem folderItem)
        {
            if (folderItem != null)
            {
                if (!fileSystemHash.ContainsKey(directory))
                    fileSystemHash.Add(directory, folderItem);
                else
                    fileSystemHash[directory] = folderItem;
            }
            else
            {
                if (fileSystemHash.ContainsKey(directory))
                    fileSystemHash.Remove(directory);
            }
        }

#if DEBUG
        internal bool HasChanges => changedEvents.Count > 0;
#endif        
    }
}
