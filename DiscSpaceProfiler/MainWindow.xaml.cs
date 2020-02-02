using DevExpress.Xpf.Grid;
using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DiscSpaceProfiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ScanCompleted += ehScanCompleted;
            viewModel.Run(@"C:\Addins");
            updateTimer = new Timer();
            updateTimer.Interval = 2000;
            updateTimer.Elapsed += ehUpdateTreeList;
            updateTimer.Start();
        }
        Timer updateTimer;
        void ehScanCompleted(object sender, EventArgs e)
        {
            //System.Windows.MessageBox.Show("Scan finished");
        }
        void ehUpdateTreeList(object sender, ElapsedEventArgs e)
        {
            DispatcherHelper.Invoke(() => {
                TreeList.BeginDataUpdate();
                TreeList.EndDataUpdate();
            });
        }
    }
    public static class ImagesHelper 
    {
        static Dictionary<string, ImageSource> fileIcons = new Dictionary<string, ImageSource>();
        static ImageSource folderIcon = null;
        static ImageSource driveIcon = null;
        static ImageSource GetIconInternal(string path, ItemType itemType)
        {
            using (var icon = ShellManager.GetIcon(path, itemType))
            {
                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(16, 16));
            }
        }
        public static ImageSource GetFolderIcon(FolderItem folderItem)
        {
            if (folderItem.Parent == null)
                return GetIconInternal(folderItem.Path, ItemType.Folder);
            if (folderIcon == null)
                folderIcon = GetIconInternal(folderItem.Path, ItemType.Folder);
            return folderIcon;
        }

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
    }
    public class FileSystemItemImageSelector : TreeListNodeImageSelector
    {
        public override ImageSource Select(DevExpress.Xpf.Grid.TreeList.TreeListRowData rowData)
        {
            var fileSystemItem = rowData.Row as FileSystemItem;
            return ImagesHelper.GetImage(fileSystemItem);
        }
    }
    

}
