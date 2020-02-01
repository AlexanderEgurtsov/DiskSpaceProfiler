using System;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class DriveItem : FolderItem
    {
        public DriveItem(string path) : base(path, path)
        {
        }
    }
}
