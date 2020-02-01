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
        Dictionary<string, FolderItem> fileSystemHash = new Dictionary<string, FolderItem>();
        ConcurrentQueue<FolderItem> foldersToScan = new ConcurrentQueue<FolderItem>();
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

        public static string GetName(string path)
        {
            return string.Intern(Path.GetFileName(path));
        }

        public void Run(string rootFolder)
        {
            scanCompleted = false;
            RunForFolder(rootFolder);
            scanMonitorTimer = new Timer(100);
            scanMonitorTimer.Elapsed += ehScanMonitorTimerElapsed;
            scanMonitorTimer.Start();

        }

        void AddFolderToScan(FolderItem item)
        {
            item.IsProcessing = true;
            foldersToScan.Enqueue(item);
        }

        IEnumerable<FileSystemItem> ProcessContent(IEnumerable<FileSystemItem> directoryContent)
        {
            foreach (FileSystemItem fileSystemItem in directoryContent)
            {
                if (fileSystemItem is FolderItem folderItem)
                {
                    yield return fileSystemItem;
                    UpdateSearchInfo(folderItem.Path, folderItem);
                    AddFolderToScan(folderItem);
                }
                else
                    yield return fileSystemItem;
            }
        }
        void CollectNestedItems(FolderItem parentItem)
        {
            if (parentItem == null || parentItem.IsFile)
                return;
            (parentItem as FolderItem).IsProcessing = true;
            var parentPath = parentItem.Path;
            if (string.IsNullOrEmpty(parentPath))
                return;
            parentItem.AddChildrenRange(ProcessContent(fileSystemDataProvider.GetDirectoryContent(parentPath)));
            parentItem.IsProcessing = false;
            if (!parentItem.HasChildren)
            {
                parentItem.UpdateIsValid(true);
            }
        }
        void ehChanged(object sender, FileSystemChangeEventArgs e)
        {
            changedEvents.Enqueue(e);
            //Debug.WriteLine($"{e.Name} {e.Path} {e.ChangeType}");
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
        void ProcessChange(FolderItem parentItem, FileSystemChangeEventArgs change)
        {
            if (change == null)
                return;
            switch (change.ChangeType)
            {
                case FileSystemChangeType.Change:
                    ProcessChange(parentItem, change.Path, change.Name);
                    break;
                case FileSystemChangeType.Deletion:
                    var deletedItem = parentItem.RemoveChildren(change.Name);
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

        void ProcessChange(FolderItem parentItem, string path, string name)
        {
            if (!fileSystemDataProvider.FileExists(path))
                return;
            var fileInfo = fileSystemDataProvider.GetFileInfo(path);
            if (fileInfo == null)
                return;
            var fileItem = parentItem.FindChildren(name);
            if (fileItem == null)
                return;
            if (fileItem.Size != fileInfo.Item2)
                fileItem.SetSize(fileInfo.Item2);
        }

        void ProcessChangeForFolder(string parentPath, FileSystemChangeEventArgs change)
        {
            if (string.IsNullOrEmpty(parentPath) || change == null)
                return;
            var parentItem = FindItem(parentPath) as FolderItem;
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
        void ProcessCreation(FolderItem parentItem, FileSystemChangeEventArgs change)
        {
            var path = change.Path;
            if (fileSystemDataProvider.FileExists(path))
            {
                
                var fileInfo = fileSystemDataProvider.GetFileInfo(path);
                if (fileInfo == null)
                    return;
                var fileItem = new FileItem(change.Name, fileInfo.Item2);

                if (parentItem.FindChildren(fileItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(fileItem);
            }
            else if (fileSystemDataProvider.DirectoryExists(path))
            {
                var folderItem = new FolderItem(path, change.Name);
                if (parentItem.FindChildren(folderItem.DisplayName) != null)
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
            var folderItem = new FolderItem(rootFolder, GetName(rootFolder));
            rootNodes.Add(folderItem);
            OnPropertyChanged(nameof(RootNodes));
            UpdateSearchInfo(rootFolder, folderItem);
            StartListeningForChanges();
            CollectNestedItems(folderItem);
        }

        private void StartListeningForChanges()
        {
            if (!watcher.Active)
            {
                watcher.Changed += ehChanged;
                Task.Run(ProcessChangesTask);
                watcher.Start((RootNodes.FirstOrDefault() as FolderItem).Path);
            }
        }

        private void UpdateSearchInfo(string directory, FolderItem folderItem)
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
