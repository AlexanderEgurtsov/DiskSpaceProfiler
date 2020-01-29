using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class FileItem : FileSystemItem
    {
        public FileItem(string path, string displayName, long size) : base(path, displayName)
        {
            Size = size;
            IsValid = true;
        }
        public override bool IsFile => true;
        [ExcludeFromCodeCoverage]
        public override void AddChildren(FileSystemItem fileItem)
        { 
        }
        [ExcludeFromCodeCoverage]
        public override FileSystemItem FindChildren(string path, string name) => null;
        [ExcludeFromCodeCoverage]
        public override FileSystemItem RemoveChildren(string path, string name) => null;
        [ExcludeFromCodeCoverage]
        public override FileSystemItem RenameChildren(string oldName, string oldPath, string name, string path) => null;
    }
}
