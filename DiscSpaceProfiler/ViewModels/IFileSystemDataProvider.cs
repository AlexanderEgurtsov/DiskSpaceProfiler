using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public interface IFileSystemDataProvider
    {
        IEnumerable<Tuple<string, long>> GetDrives();
        bool DirectoryExists(string path); 
        bool FileExists(string path);
        IEnumerable<string> GetDirectories(string path);
        IEnumerable<Tuple<string, long>> GetFiles(string path);
        Tuple<string, long> GetFileInfo(string path);
    }
}
