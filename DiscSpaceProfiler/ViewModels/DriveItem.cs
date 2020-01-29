using System;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class DriveItem : FileSystemItemWithChildren
    {
        public DriveItem(string path) : base(path, path)
        {
        }
    }
}
