using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DiscSpaceProfiler.ViewModels;

namespace DiscSpaceProfiler.Code.FileSystem
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
        public object VisualObject { get; set; }
        public FileSystemItem Parent { get; private set; }        
        public long Size { get; protected set; }
        long lastReportedSize;

        public string GetPath()
        {
            if (Parent == null)
                return DisplayName;
            return System.IO.Path.Combine(Parent.GetPath(), DisplayName);
        }
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
            OnPropertyChanged(nameof(Size));
        }
        protected void OnPropertyChanged(string propertyName)
        {
            if (propertyName == nameof(Size))
                lastReportedSize = Size;
            if (PropertyChanged == null)
                return;
            DispatcherHelper.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
        protected void UpdateSize(long sizeDelta)
        {
            lock (this)
                Size += sizeDelta;
            if (Math.Abs(Size - lastReportedSize) > 500000000)
            {
                lastReportedSize = Size;
                OnPropertyChanged(nameof(Size));
            }
            FolderItem parentItem = Parent as FolderItem;
            parentItem?.UpdateSize(sizeDelta);
        }
    }
}
