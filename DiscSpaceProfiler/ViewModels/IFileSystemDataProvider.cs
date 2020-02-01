using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public interface IFileSystemDataProvider
    {
        bool DirectoryExists(string path); 
        bool FileExists(string path);
        IEnumerable<FileSystemItem> GetDirectoryContent(string path);
        Tuple<string, long> GetFileInfo(string path);
    }
}
