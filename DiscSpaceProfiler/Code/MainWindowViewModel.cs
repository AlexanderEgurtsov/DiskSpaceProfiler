﻿using DiscSpaceProfiler.Code.FileSystem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DiscSpaceProfiler.ViewModels
{
    using Timer = System.Timers.Timer;

    public class FileSystemItemChangedEventArgs : EventArgs
    {
        public FileSystemItemChangedEventArgs(FolderItem parentItem, FileSystemItem item)
        {
            Parent = parentItem;
            Item = item;
        }

        public FileSystemItem Item { get; private set; }
        public FolderItem Parent { get; private set; }
    }
    public class FileSystemItemProcessedEventArgs : EventArgs
    {
        public FileSystemItemProcessedEventArgs(FileSystemItem item)
        {
            Item = item;
        }

        public FileSystemItem Item { get; private set; }
    }
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        const int INT_MaxTaskCount = 3;
        List<Task> activeTasks = new List<Task>();
        ConcurrentQueue<FileSystemChangeEventArgs> changedEvents = new ConcurrentQueue<FileSystemChangeEventArgs>();
        IFileSystemDataProvider fileSystemProvider;
        IFileSystemWatcher fileSystemWatcher;
        CancellationTokenSource folderScanTokenSource;
        ConcurrentQueue<FolderItem> foldersToScan = new ConcurrentQueue<FolderItem>();
        int maxTasksCount;
        Task processChangesTask;
        CancellationTokenSource processChangesTokenSource;
        bool processingIsActive;
        Timer scanMonitorTimer;

        public MainWindowViewModel()
        {
            fileSystemProvider = new DefaultFileSystemDataProvider();
            fileSystemWatcher = new DefaultFileSystemWatcher();
            maxTasksCount = INT_MaxTaskCount;
            scanMonitorTimer = new Timer(100);
            scanMonitorTimer.Elapsed += ehScanMonitorTimerElapsed;
            fileSystemWatcher.Changed += ehChanged;
            folderScanTokenSource = new CancellationTokenSource();
        }

        public event EventHandler<FileSystemItemChangedEventArgs> FileSystemItemAdded;
        public event EventHandler<FileSystemItemChangedEventArgs> FileSystemItemDeleted;
        public event EventHandler<FileSystemItemProcessedEventArgs> FileSystemItemProcessed;
        public event EventHandler<FileSystemItemProcessedEventArgs> FolderSizeCalculated;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool ProcessingIsActive
        {
            get
            {
                return processingIsActive;
            }
            set
            {
                if (processingIsActive == value)
                    return;
                processingIsActive = value;
                OnPropertyChanged(nameof(processingIsActive));
            }
        }
        public FolderItem RootNode { get; private set; }
        public string RootPath
        {
            get
            {
                if (RootNode == null)
                    return string.Empty;
                return RootNode.DisplayName;
            }
        }

        public static string GetName(string path)
        {
            return string.Intern(Path.GetFileName(path));
        }

        public FileSystemItem FindItem(string parentPath)
        {
            if (!parentPath.StartsWith(RootPath))
                return null;
            if (parentPath == RootPath)
                return RootNode;
            parentPath = parentPath.Remove(0, RootPath.Length);
            if (string.IsNullOrEmpty(parentPath))
                return null;
            if (parentPath[0] == Path.DirectorySeparatorChar)
                parentPath = parentPath.Remove(0, 1);
            if (string.IsNullOrEmpty(parentPath))
                return null;
            var childNames = parentPath.Split(Path.DirectorySeparatorChar);
            if (childNames == null || childNames.Length == 0)
                return null;
            var currentNode = RootNode;
            if (currentNode == null)
                return null;
            for (int i = 0; i < childNames.Length; i++)
            {
                currentNode = currentNode.FindChildren(childNames[i]) as FolderItem;
                if (currentNode == null)
                    return null;
            }
            return currentNode;
        }
        public void Run(string rootFolder)
        {
            StopProcessing();
            RunForFolder(rootFolder);
            StartScan();
        }
        public void StopProcessing()
        {
            fileSystemWatcher.Stop();
            if (processChangesTask != null && !processChangesTask.IsCompleted && !processChangesTask.IsCanceled)
                folderScanTokenSource?.Cancel();

            scanMonitorTimer.Stop();
            if (!folderScanTokenSource.IsCancellationRequested)
                folderScanTokenSource.Cancel();
            lock (activeTasks)
                activeTasks.Clear();
            while (foldersToScan.TryDequeue(out _))
            {

            }
            while (changedEvents.TryDequeue(out _))
            {

            }
            ProcessingIsActive = false;
        }
        void AddFolderToScan(FolderItem item)
        {
            item.IsProcessing = true;
            foldersToScan.Enqueue(item);
        }
        void CollectNestedItems(FolderItem parentItem, CancellationToken cancellationToken)
        {
            if (parentItem == null || parentItem.IsFile)
                return;
            if (cancellationToken.IsCancellationRequested)
                return;
            (parentItem as FolderItem).IsProcessing = true;
            var parentPath = parentItem.GetPath();
            if (string.IsNullOrEmpty(parentPath))
                return;
            parentItem.AddChildrenRange(ProcessContent(fileSystemProvider.GetDirectoryContent(parentPath), cancellationToken));
            if (cancellationToken.IsCancellationRequested)
                return;
            parentItem.IsProcessing = false;
            if (!parentItem.HasChildren)
                parentItem.UpdateIsValid(true);
            OnFileSystemItemProcessed(parentItem);

        }
        void ehChanged(object sender, FileSystemChangeEventArgs e)
        {
            changedEvents.Enqueue(e);
            if (processChangesTask == null || processChangesTask.IsCompleted || processChangesTask.IsCanceled)
            {
                processChangesTokenSource = new System.Threading.CancellationTokenSource();
                processChangesTask = Task.Run(ProcessChangesTask, processChangesTokenSource.Token);
            }
        }
        void ehScanMonitorTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (foldersToScan.Count == 0)
                return;
            lock (activeTasks)
            {
                if (activeTasks.Count == 0)
                    for (int i = 0; i < maxTasksCount; i++)
                        activeTasks.Add(Task.Run(() => FolderScanTask(folderScanTokenSource.Token)));
                for (int i = 0; i < activeTasks.Count; i++)
                {
                    Task task = activeTasks[i];
                    if (task.IsCompleted || task.IsCanceled)
                        activeTasks[i] = Task.Run(() => FolderScanTask(folderScanTokenSource.Token));
                }
            }
        }
        private void ehPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            FolderItem folderItem = sender as FolderItem;
            if (folderItem == null || !folderItem.IsValid)
                return;
            if (e.PropertyName != nameof(FileSystemItem.IsValid))
                return;
            OnFolderSizeCalculated(folderItem);
        }
        void FolderScanTask(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            while (foldersToScan.TryDequeue(out var currentItem))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                CollectNestedItems(currentItem, cancellationToken);
            }
        }
        bool HasActiveTasks()
        {
            lock (activeTasks)
            {
                for (int i = 0; i < activeTasks.Count; i++)
                {
                    Task task = activeTasks[i];
                    if (!(task.IsCompleted || task.IsCanceled))
                        return true;
                }
            }
            return false;
        }
        void OnFileSystemItemAdded(FolderItem parentItem, FileSystemItem addedItem)
        {
            FileSystemItemAdded?.Invoke(this, new FileSystemItemChangedEventArgs(parentItem, addedItem));
        }
        void OnFileSystemItemDeleted(FolderItem parentItem, FileSystemItem deletedItem)
        {
            FileSystemItemDeleted?.Invoke(this, new FileSystemItemChangedEventArgs(parentItem, deletedItem));
        }
        void OnFileSystemItemProcessed(FolderItem item)
        {
            FileSystemItemProcessed?.Invoke(this, new FileSystemItemProcessedEventArgs(item));
        }
        void OnFolderSizeCalculated(FolderItem item)
        {
            FolderSizeCalculated?.Invoke(this, new FileSystemItemProcessedEventArgs(item));
        }
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                    if (deletedItem != null)
                    {
                        if (deletedItem is FolderItem deletedFolder)
                            deletedFolder.IsProcessing = false;
                        OnFileSystemItemDeleted(parentItem, deletedItem);
                    }
                    break;
                case FileSystemChangeType.Creation:
                    ProcessCreation(parentItem, change);
                    break;
                case FileSystemChangeType.Rename:
                    parentItem.RenameChildren(change.OldName, change.OldPath, change.Name, change.Path);
                    break;
            }
        }
        void ProcessChange(FolderItem parentItem, string path, string name)
        {
            if (!fileSystemProvider.FileExists(path))
                return;
            var fileInfo = fileSystemProvider.GetFileInfo(path);
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
            if (string.IsNullOrEmpty(parentPath))
                return;
            var parentItem = FindItem(parentPath) as FolderItem;
            if (parentItem == null)
                return;
            while (parentItem.IsProcessing)
            {
                if (processChangesTokenSource != null && processChangesTokenSource.IsCancellationRequested)
                    return;
            }
            ProcessChange(parentItem, change);
        }
        void ProcessChangesTask()
        {
            while (changedEvents.TryDequeue(out var change))
            {
                var parentPath = Path.GetDirectoryName(change.Path);
                ProcessChangeForFolder(parentPath, change);
            }
        }
        IEnumerable<FileSystemItem> ProcessContent(IEnumerable<FileSystemItem> directoryContent, CancellationToken cancellationToken)
        {
            foreach (FileSystemItem fileSystemItem in directoryContent)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                if (fileSystemItem is FolderItem folderItem)
                {
                    yield return fileSystemItem;
                    AddFolderToScan(folderItem);
                    folderItem.PropertyChanged += ehPropertyChanged;
                }
                else
                    yield return fileSystemItem;
            }
        }
        void ProcessCreation(FolderItem parentItem, FileSystemChangeEventArgs change)
        {
            var path = change.Path;
            if (fileSystemProvider.FileExists(path))
            {
                var fileInfo = fileSystemProvider.GetFileInfo(path);
                if (fileInfo == null)
                    return;
                var fileItem = new FileItem(change.Name, fileInfo.Item2);
                if (parentItem.FindChildren(fileItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(fileItem);
                OnFileSystemItemAdded(parentItem, fileItem);
            }
            else if (fileSystemProvider.DirectoryExists(path))
            {
                var folderItem = new FolderItem(change.Name);
                if (parentItem.FindChildren(folderItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(folderItem);
                folderItem.PropertyChanged += ehPropertyChanged;
                AddFolderToScan(folderItem);
                OnFileSystemItemAdded(parentItem, folderItem);
            }
        }
        void RunForFolder(string rootFolder)
        {
            RootNode = null;
            folderScanTokenSource = new CancellationTokenSource();
            var folderItem = new FolderItem(rootFolder);
            RootNode = folderItem;
            folderItem.PropertyChanged += ehPropertyChanged;
            OnPropertyChanged(nameof(RootNode));
            OnPropertyChanged(nameof(RootPath));
            StartListeningForChanges();
            CollectNestedItems(folderItem, folderScanTokenSource.Token);
            ProcessingIsActive = true;
        }
        private void StartListeningForChanges()
        {
            fileSystemWatcher.Start(RootNode.GetPath());
        }
        private void StartScan()
        {
            scanMonitorTimer.Start();
        }
#if DEBUG
        internal bool HasChanges => changedEvents.Count > 0;
        internal bool IsScanning => foldersToScan.Count > 0 || HasActiveTasks();
#endif        
    }
}
