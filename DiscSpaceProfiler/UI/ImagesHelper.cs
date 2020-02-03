using DiscSpaceProfiler.Code.FileSystem;
using DiscSpaceProfiler.UI.Interop;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiscSpaceProfiler.UI
{
    public static class ImagesHelper
    {
        static Dictionary<string, ImageSource> fileIcons = new Dictionary<string, ImageSource>();
        static ImageSource folderIcon = null;

        public static ImageSource GetFileIcon(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName);
            if (!fileIcons.TryGetValue(ext, out var fileIcon))
            {
                fileIcon = GetIconInternal(ext, ItemType.File);
                fileIcons.Add(ext, fileIcon);
            }
            return fileIcon;
        }
        public static ImageSource GetFolderIcon(FolderItem folderItem)
        {
            if (folderItem.Parent == null)
                return GetIconInternal(folderItem.GetPath(), ItemType.Folder);
            if (folderIcon == null)
                folderIcon = GetIconInternal(folderItem.GetPath(), ItemType.Folder);
            return folderIcon;
        }
        public static ImageSource GetImage(FileSystemItem item)
        {
            if (item == null)
                return null;
            if (item.IsFile)
                return GetFileIcon(item.DisplayName);
            if (item is FolderItem folderItem)
                return GetFolderIcon(folderItem);
            return null;
        }

        static ImageSource GetIconInternal(string path, ItemType itemType)
        {
            using (var icon = ShellManager.GetIcon(path, itemType))
            {
                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(16, 16));
            }
        }
    }
}
