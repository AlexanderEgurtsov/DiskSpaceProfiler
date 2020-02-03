using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscSpaceProfiler.Code.FileSystem
{
    public interface IFileSystemDataProvider
    {
        bool DirectoryExists(string path); 
        bool FileExists(string path);
        IEnumerable<FileSystemItem> GetDirectoryContent(string path);
        Tuple<string, long> GetFileInfo(string path);
    }
}
