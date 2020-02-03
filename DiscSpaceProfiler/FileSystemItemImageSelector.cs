using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Linq;
using System.Windows.Media;

namespace DiscSpaceProfiler
{
    public class FileSystemItemImageSelector : TreeListNodeImageSelector
    {
        public override ImageSource Select(DevExpress.Xpf.Grid.TreeList.TreeListRowData rowData)
        {
            var fileSystemItem = rowData.Row as FileSystemItem;
            return ImagesHelper.GetImage(fileSystemItem);
        }
    }
}
