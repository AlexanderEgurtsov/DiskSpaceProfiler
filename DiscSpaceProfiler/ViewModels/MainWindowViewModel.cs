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
        Dictionary<string, FileSystemItemWithChildren> fileSystemHash = new Dictionary<string, FileSystemItemWithChildren>();
        ConcurrentQueue<FileSystemItemWithChildren> foldersToScan = new ConcurrentQueue<FileSystemItemWithChildren>();
        int maxNestingLevel;
        int maxTasksCount;
        List<FileSystemItem> rootNodes = new List<FileSystemItem>();
        Timer scanMonitorTimer;
        IFileSystemWatcher watcher;
        bool scanCompleted;

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
            scanCompleted = false;
            if (string.IsNullOrEmpty(rootFolder))
                RunForLocalDrive();
            else
                RunForFolder(rootFolder);
            scanMonitorTimer = new Timer(100);
            scanMonitorTimer.Elapsed += ehScanMonitorTimerElapsed;
            scanMonitorTimer.Start();
            
        }

        void AddFolderToScan(FileSystemItemWithChildren item)
        {
            item.IsProcessing = true;
            foldersToScan.Enqueue(item);
        }
        
        void CollectNestedItems(FileSystemItemWithChildren parentItem)
        {
            if (parentItem == null || parentItem.IsFile)
                return;
            (parentItem as FileSystemItemWithChildren).IsProcessing = true;
            var parentPath = parentItem.Path;
            if (string.IsNullOrEmpty(parentPath) || !fileSystemDataProvider.DirectoryExists(parentPath))
                return;
            var directories = fileSystemDataProvider.GetDirectories(parentPath);
            var files = fileSystemDataProvider.GetFiles(parentPath);
            foreach (string directory in directories)
            {
                FolderItem folderItem = new FolderItem(directory, Path.GetFileName(directory));
                parentItem.AddChildren(folderItem);
                UpdateSearchInfo(directory, folderItem);
                AddFolderToScan(folderItem);
            }
            foreach (var fileInfo in files)
            {
                FileItem fileItem = new FileItem(fileInfo.Item1, Path.GetFileName(fileInfo.Item1), fileInfo.Item2);
                parentItem.AddChildren(fileItem);
            }
            parentItem.IsProcessing = false;
            if (!parentItem.HasChildren)
            {
                parentItem.UpdateIsValid(true);
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
            CollectNestedItems(currentItem);
        }

        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        void OnScanCompleted()
        {
            scanCompleted = true;
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
            var parentItem = FindItem(parentPath) as FileSystemItemWithChildren;
            if (parentItem == null)
                return;
            while (parentItem.IsProcessing)
            {
                
            }
            ProcessChange(parentItem, change);
        }
        void ProcessChangesTask()
        {
            while (true)
            {
                if (!changedEvents.TryDequeue(out var change))
                    continue;

                var parentPath = Path.GetDirectoryName(change.Path);
                ProcessChangeForFolder(parentPath, change);
            }
        }
        void ProcessCreation(FileSystemItem parentItem, FileSystemChangeEventArgs change)
        {
            var path = change.Path;
            var parentItemCasted = parentItem as FileSystemItemWithChildren;
            if (fileSystemDataProvider.FileExists(path))
            {
                
                var fileInfo = fileSystemDataProvider.GetFileInfo(path);
                if (fileInfo == null)
                    return;
                var fileItem = new FileItem(path, change.Name, fileInfo.Item2);

                if (parentItem.FindChildren(fileItem.Path, fileItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(fileItem);
            }
            else if (fileSystemDataProvider.DirectoryExists(path))
            {
                var folderItem = new FolderItem(path, change.Name);
                if (parentItem.FindChildren(folderItem.Path, folderItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(folderItem);
                
                UpdateSearchInfo(path, folderItem);
                
                AddFolderToScan(folderItem);
                scanCompleted = false;
                scanMonitorTimer.Start();
            }
        }

        void RunForFolder(string rootFolder)
        {
            rootNodes.Clear();
            var folderItem = new FolderItem(rootFolder, Path.GetFileName(rootFolder));
            rootNodes.Add(folderItem);
            OnPropertyChanged(nameof(RootNodes));
            UpdateSearchInfo(rootFolder, folderItem);
            StartListeningForChanges();
            CollectNestedItems(folderItem);
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
            StartListeningForChanges();
            CollectNestedItems(driveItem);
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

        private void UpdateSearchInfo(string directory, FileSystemItemWithChildren folderItem)
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
        internal bool HasTasksToScan => foldersToScan.Count > 0 && scanMonitorTimer.Enabled;
#endif        
    }
}
