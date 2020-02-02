using System;
using System.IO;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class DefaultFileSystemWatcher : IFileSystemWatcher
    {
        FileSystemWatcher watcher;

        public bool Active => watcher != null && watcher.EnableRaisingEvents;

        public event EventHandler<FileSystemChangeEventArgs> Changed;

        void ehChanged(object sender, FileSystemEventArgs e)
        {
            Changed?.Invoke(this, new FileSystemChangeEventArgs(MainWindowViewModel.GetName(e.Name), e.FullPath, FileSystemChangeType.Change));
        }
        void ehCreated(object sender, FileSystemEventArgs e)
        {
            Changed?.Invoke(this, new FileSystemChangeEventArgs(MainWindowViewModel.GetName(e.Name), e.FullPath, FileSystemChangeType.Creation));
        }
        void ehDeleted(object sender, FileSystemEventArgs e)
        {
            Changed?.Invoke(this, new FileSystemChangeEventArgs(MainWindowViewModel.GetName(e.Name), e.FullPath, FileSystemChangeType.Deletion));
        }
        void ehRenamed(object sender, RenamedEventArgs e)
        {
            Changed?.Invoke(this, new FileSystemChangeEventArgs(MainWindowViewModel.GetName(e.Name), e.FullPath, MainWindowViewModel.GetName(e.OldName), e.OldFullPath));
        }
        public void Start(string path)
        {
            watcher = new FileSystemWatcher() { Path = path, IncludeSubdirectories = true};
            watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.CreationTime;
            watcher.InternalBufferSize = watcher.InternalBufferSize;
            watcher.Changed += ehChanged;
            watcher.Created += ehCreated;
            watcher.Deleted += ehDeleted;
            watcher.Renamed += ehRenamed;
            watcher.EnableRaisingEvents = true;
        }
        public void Stop()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= ehChanged;
                watcher.Created -= ehCreated;
                watcher.Deleted -= ehDeleted;
                watcher = null;
            }
        }
    }
}
