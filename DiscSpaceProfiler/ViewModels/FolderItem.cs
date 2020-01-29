using System;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class FolderItem : FileSystemItemWithChildren
    {
        public FolderItem(string path, string displayName) : base(path, displayName)
        {

        }
    }
}
