using System;
using System.Linq;

namespace DiscSpaceProfiler.Code.FileSystem
{
    public enum FileSystemChangeType
    {
        Change,
        Deletion,
        Creation,
        Rename
    }
    public class FileSystemChangeEventArgs : EventArgs
    {
        public FileSystemChangeEventArgs(string newName, string newPath, FileSystemChangeType changeType)
        {
            this.Name = newName;
            this.Path = newPath;
            this.ChangeType = changeType;
        }
        public FileSystemChangeEventArgs(string newName, string newPath, string oldName, string oldPath)
        {
            this.Name = newName;
            this.Path = newPath;
            this.OldName = oldName;
            this.OldPath = oldPath;
            this.ChangeType = FileSystemChangeType.Rename;
        }
        public string Name { get; set; }
        public string Path { get; set; }
        public string OldName { get; set; }
        public string OldPath { get; set; }
        public FileSystemChangeType ChangeType { get; set; }
    }
    public interface IFileSystemWatcher
    {
        void Start(string path);
        void Stop();
        event EventHandler<FileSystemChangeEventArgs> Changed;
    }
}
