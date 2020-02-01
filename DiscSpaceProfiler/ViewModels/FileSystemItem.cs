﻿using DevExpress.Xpf.Grid;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using System.Threading.Tasks;
using System.Timers;

namespace DiscSpaceProfiler.ViewModels
{
    public abstract class FileSystemItem : INotifyPropertyChanged
    {

        public FileSystemItem(string displayName)
        {
            DisplayName = displayName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual IEnumerable<FileSystemItem> Children => Enumerable.Empty<FileSystemItem>();
        public string DisplayName { get; private set; }
        public virtual bool HasChildren => false;
        public virtual bool IsFile => false;
        public virtual bool IsValid => true;

        public FileSystemItem Parent { get; private set; }
        public long Size { get; protected set; }

        public void SetDisplayName(string displayName)
        {
            DisplayName = displayName;
            OnPropertyChanged(nameof(DisplayName));
        }

        public void SetParent(FileSystemItem parent)
        {
            Parent = parent;
        }

        public void SetSize(long newSize)
        {
            var sizeDelta = newSize - this.Size;
            UpdateSize(sizeDelta);
        }
        
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged == null)
                return;
            DispatcherHelper.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
        protected void UpdateSize(long size)
        {
            lock (this)
                Size += size;

            FolderItem parentItem = Parent as FolderItem;
            parentItem?.UpdateSize(size);
        }


    }
}
