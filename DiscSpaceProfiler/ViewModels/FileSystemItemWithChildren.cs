﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public abstract class FileSystemItemWithChildren : FileSystemItem
    {
        List<FileItem> files;
        List<FolderItem> folders;

        public FileSystemItemWithChildren(string path, string displayName) : base(path, displayName)
        {

        }

        public override IEnumerable<FileSystemItem> Children
        {
            get
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
            if (folders == null)
                return true;
            foreach (FolderItem folderItem in folders)
            {
                if (!folderItem.IsValid)
                    return false;
            }
            return true;
        }
        public void UpdateIsValid(bool childrenIsValid)
        {
            bool oldIsValid = IsValid;
            if (oldIsValid == childrenIsValid)
                return;
            if (!childrenIsValid)
                IsValid = false;
            else
                IsValid = FoldersAreValid();
            if (oldIsValid != IsValid)
                (Parent as FileSystemItemWithChildren)?.UpdateIsValid(this.IsValid);
        }
        public override void AddChildren(FileSystemItem childItem)
        {
            childItem.SetParent(this);
            if (childItem is FileItem fileItem)
            {
                AddFile(fileItem);
            }
            else
                if (childItem is FolderItem folderItem)
            {
                AddFolder(folderItem);
            }
            UpdateSize(childItem.Size);
            UpdateIsValid(childItem.IsValid);
            OnPropertyChanged(nameof(Children));
        }
        public override bool HasChildren => (files != null && files.Count > 0) || (folders != null && folders.Count > 0);
        public override FileSystemItem FindChildren(string path, string name)
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
            return null;
        }
        public override FileSystemItem RemoveChildren(string path, string name) 
        {
            var children = FindChildren(path, name);
            if (children == null)
                return null;
            if (children is FileItem fileItem)
                files.Remove(fileItem);
            else if (children is FolderItem folderItem)
                folders.Remove(folderItem);
            UpdateSize(-children.Size);
            OnPropertyChanged(nameof(Children));
            return children;
        }
        public override FileSystemItem RenameChildren(string oldName, string oldPath, string name, string path) 
        {
            var children = FindChildren(oldPath, oldName);
            if (children == null)
                return null;
            children.SetPath(path);
            children.SetDisplayName(name);
            return children;
        }

    }
}
