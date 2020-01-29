using DevExpress.Xpf.Grid;
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
        public FileSystemItem(string path, string displayName)
        {
            Path = path;
            DisplayName = displayName;
        }
        public void SetPath(string path)
        {
            Path = path;
            OnPropertyChanged(nameof(Path));
        }
        public void SetDisplayName(string displayName)
        {
            DisplayName = displayName;
            OnPropertyChanged(nameof(DisplayName));
        }
        public virtual IEnumerable<FileSystemItem> Children => Enumerable.Empty<FileSystemItem>();
        public string DisplayName { get; private set; }
        public virtual bool HasChildren => false;
        public virtual bool IsFile => false;
        bool isValid;
        public bool IsValid
        {
            get
            {
                return this.isValid;
            }
            protected set
            {
                if (this.isValid == value)
                {
                    return;
                }

                this.isValid = value;
                this.OnPropertyChanged(nameof(this.IsValid));
                if (this.isValid)
                    this.OnPropertyChanged(nameof(this.Size));
            }
        }
        public FileSystemItem Parent { get; private set; }
        public string Path { get; private set; }
        long size;
        public long Size
        {
            get
            {
                return this.size;
            }
            protected set
            {
                if (this.size == value)
                {
                    return;
                }

                this.size = value;
                //this.OnPropertyChanged(nameof(this.Size));
            }
        }
        protected void OnPropertyChanged(string propertyName) 
        {
            if (PropertyChanged == null)
                return;
            DispatcherHelper.Invoke(()=>PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public abstract void AddChildren(FileSystemItem fileItem);
        
        public void SetParent(FileSystemItem parent)
        {
            Parent = parent;
        }
        public override string ToString() => Path;
        public abstract FileSystemItem RenameChildren(string oldName, string oldPath, string name, string path);
        public abstract FileSystemItem RemoveChildren(string path, string name);
        public abstract FileSystemItem FindChildren(string path, string name);
        public void SetSize(long newSize)
        {
            var sizeDelta = newSize - size;
            UpdateSize(sizeDelta);
        }
        protected void UpdateSize(long size)
        {
            Size += size;
            this.OnPropertyChanged(nameof(Size));
            FileSystemItemWithChildren parentItem = Parent as FileSystemItemWithChildren;
            while (parentItem != null)
            {
                parentItem.Size += size;
                parentItem.OnPropertyChanged(nameof(Size));
                parentItem = parentItem.Parent as FileSystemItemWithChildren;
            }
        }
        

    }
}
