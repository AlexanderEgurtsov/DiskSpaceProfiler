using System;
using System.Linq;

namespace DiscSpaceProfiler.Code.FileSystem
{
    public class FileItem : FileSystemItem
    {
        public FileItem(string displayName, long size) : base(displayName)
        {
            Size = size;
        }
        public override bool IsFile => true;
    }
}
