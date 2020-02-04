using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscSpaceProfiler.Code.FileSystem
{
    public class FolderItem : FileSystemItem
    {
        object childrenLock = new object();
        List<FileItem> files;
        List<FolderItem> folders;
        bool isValid;

        public FolderItem(string displayName) : base(displayName)
        {
        }

        public override IEnumerable<FileSystemItem> Children
        {
            get
            {
                lock (childrenLock)
                {
                    if (folders != null)
                        foreach (FolderItem folderItem in folders)
                        {
                            yield return folderItem;
                        }
                    if (files != null)
                        foreach (FileItem fileItem in files)
                        {
                            yield return fileItem;
                        }
                }
            }
        }
        public override bool HasChildren
        {
            get
            {
                lock (childrenLock)
                    return (files != null && files.Count > 0) || (folders != null && folders.Count > 0);
            }
        }
        public bool IsProcessing { get; set; }
        public override bool IsValid
        {
            get
            {
                return this.isValid;
            }
        }

        public void AddChildren(FileSystemItem childItem)
        {
            childItem.SetParent(this);
            lock (childrenLock)
            {
                if (childItem is FileItem fileItem)
                {
                    AddFile(fileItem);
                    UpdateSize(childItem.Size);
                    OnPropertyChanged(nameof(Size));
                }
                else
                    if (childItem is FolderItem folderItem)
                {
                    AddFolder(folderItem);
                }
            }
            UpdateIsValid(childItem.IsValid);
        }
        public void AddChildrenRange(IEnumerable<FileSystemItem> items)
        {
            long sizeDelta = 0;
            lock (childrenLock)
            {
                foreach (var item in items)
                {
                    item.SetParent(this);
                    if (item is FileItem fileItem)
                    {
                        AddFile(fileItem);
                        sizeDelta += fileItem.Size;
                    }
                    else if (item is FolderItem folderItem)
                        AddFolder(folderItem);
                }
            }
            UpdateSize(sizeDelta);
            UpdateIsValid(true);
        }
        public FileSystemItem FindChildren(string name)
        {
            lock (childrenLock)
            {
                if (folders != null)
                    for (int i = 0; i < folders.Count; i++)
                    {
                        var folder = folders[i];
                        if (folder.DisplayName == name)
                            return folder;
                    }
                if (files != null)
                    for (int i = 0; i < files.Count; i++)
                    {
                        var file = files[i];
                        if (file.DisplayName == name)
                            return file;
                    }
            }
            return null;
        }
        public FileSystemItem RemoveChildren(string name)
        {
            var children = FindChildren(name);
            if (children == null)
                return null;
            lock (childrenLock)
            {
                if (children is FileItem fileItem)
                    files.Remove(fileItem);
                else if (children is FolderItem folderItem)
                    folders.Remove(folderItem);
            }
            if (!children.IsValid && IsValid)
                UpdateIsValid(true);
            UpdateSize(-children.Size);
            OnPropertyChanged(nameof(Size));
            return children;
        }
        public void RenameChildren(string oldName, string oldPath, string name, string path)
        {
            var children = FindChildren(oldName);
            if (children == null)
                return;
            children.SetDisplayName(name);
        }

        public void UpdateIsValid(bool childrenIsValid)
        {
            bool oldIsValid = false;
            lock (this)
            {
                oldIsValid = IsValid;
                if (oldIsValid == childrenIsValid)
                    return;
                if (!childrenIsValid)
                    isValid = false;
                else
                    isValid = FoldersAreValid();
            }
            if (oldIsValid != IsValid)
            {
                OnPropertyChanged(nameof(IsValid));
                if (IsValid)
                {
                    OnPropertyChanged(nameof(Size));
                }
                (Parent as FolderItem)?.UpdateIsValid(this.IsValid);
            }
        }
        void AddFile(FileItem fileItem)
        {
            if (files == null)
                files = new List<FileItem>();
            files.Add(fileItem);
        }
        void AddFolder(FolderItem folderItem)
        {
            if (folders == null)
                folders = new List<FolderItem>();
            folders.Add(folderItem);
        }
        bool FoldersAreValid()
        {
            lock (childrenLock)
            {
                if (folders == null)
                    return true;
                for (int i = 0; i < folders.Count; i++)
                {
                    if (!folders[i].IsValid)
                        return false;
                }
            }
            return true;
        }
    }
}
