﻿using DevExpress.Xpf.Grid;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DiscSpaceProfiler.ViewModels
{
    using Timer = System.Timers.Timer;

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        const int INT_MaxTaskCount = 5;
        List<Task> activeTasks = new List<Task>();
        ConcurrentQueue<FileSystemChangeEventArgs> changedEvents = new ConcurrentQueue<FileSystemChangeEventArgs>();
        Dictionary<string, FolderItem> fileSystemHash = new Dictionary<string, FolderItem>();
        IFileSystemDataProvider fileSystemProvider;
        IFileSystemWatcher fileSystemWatcher;
        CancellationTokenSource folderScanTokenSource;
        ConcurrentQueue<FolderItem> foldersToScan = new ConcurrentQueue<FolderItem>();
        int maxTasksCount;
        Task processChangesTask;
        CancellationTokenSource processChangesTaskCancellation;
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
            RootNodes = new ObservableCollection<FolderItem>();
            folderScanTokenSource = new CancellationTokenSource();
        }

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
        public ObservableCollection<FolderItem> RootNodes { get; private set; }
        public string RootPath
        {
            get
            {
                if (RootNodes == null || RootNodes.Count == 0)
                    return string.Empty;
                return RootNodes[0].DisplayName;
            }
        }

        public static string GetName(string path)
        {
            return string.Intern(Path.GetFileName(path));
        }

        public FileSystemItem FindItem(string parentPath)
        {
            lock (fileSystemHash)
                if (fileSystemHash.TryGetValue(parentPath, out var result))
                    return result;
            return null;
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
            var parentPath = parentItem.Path;
            if (string.IsNullOrEmpty(parentPath))
                return;
            parentItem.AddChildrenRange(ProcessContent(fileSystemProvider.GetDirectoryContent(parentPath), cancellationToken));
            if (cancellationToken.IsCancellationRequested)
                return;
            parentItem.IsProcessing = false;
            if (!parentItem.HasChildren)
            {
                parentItem.UpdateIsValid(true);
            }
        }
        void ehChanged(object sender, FileSystemChangeEventArgs e)
        {
            changedEvents.Enqueue(e);
            if (processChangesTask == null || processChangesTask.IsCompleted || processChangesTask.IsCanceled)
            {
                processChangesTaskCancellation = new System.Threading.CancellationTokenSource();
                processChangesTask = Task.Run(ProcessChangesTask, processChangesTaskCancellation.Token);
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
                if (processChangesTaskCancellation != null && processChangesTaskCancellation.IsCancellationRequested)
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
                    UpdateSearchInfo(folderItem.Path, folderItem);
                    AddFolderToScan(folderItem);
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
            }
            else if (fileSystemProvider.DirectoryExists(path))
            {
                var folderItem = new FolderItem(path, change.Name);
                if (parentItem.FindChildren(folderItem.DisplayName) != null)
                    return;
                parentItem.AddChildren(folderItem);
                UpdateSearchInfo(path, folderItem);
                AddFolderToScan(folderItem);
            }
        }
        void RunForFolder(string rootFolder)
        {
            RootNodes.Clear();
            folderScanTokenSource = new CancellationTokenSource();
            var folderItem = new FolderItem(rootFolder, rootFolder);
            RootNodes.Add(folderItem);
            OnPropertyChanged(nameof(RootNodes));
            OnPropertyChanged(nameof(RootPath));
            UpdateSearchInfo(rootFolder, folderItem);
            StartListeningForChanges();
            CollectNestedItems(folderItem, folderScanTokenSource.Token);
            ProcessingIsActive = true;
        }
        private void StartListeningForChanges()
        {
            fileSystemWatcher.Start((RootNodes.FirstOrDefault() as FolderItem).Path);
        }
        private void StartScan()
        {
            scanMonitorTimer.Start();
        }
        private void UpdateSearchInfo(string directory, FolderItem folderItem)
        {
            lock (fileSystemHash)
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
        internal bool IsScanning => foldersToScan.Count > 0 || HasActiveTasks();
#endif        
    }
}
