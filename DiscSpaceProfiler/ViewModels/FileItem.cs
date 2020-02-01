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
        }
        public override bool IsFile => true;
    }
}
