using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.Code.FileSystem;
using System.Windows.Media;

namespace DiscSpaceProfiler.UI
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
