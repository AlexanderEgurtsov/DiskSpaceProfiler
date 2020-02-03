using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscSpaceProfiler.ViewModels
{
    public class DefaultFileSystemDataProvider : IFileSystemDataProvider
    {
        public bool DirectoryExists(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }
        public bool FileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }


        public Tuple<string, long> GetFileInfo(string path)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(path);
                return new Tuple<string, long>(fileInfo.Name, fileInfo.Length);
            }
            catch
            {
                return null;
            }
             
        }

        public IEnumerable<FileSystemItem> GetDirectoryContent(string path) 
        {
            IEnumerable<FileSystemInfo> enumerator = null;
            try
            {
                var dirInfo = new DirectoryInfo(path);
                if (dirInfo == null)
                    yield break;
                enumerator = dirInfo.EnumerateFileSystemInfos();
                if (enumerator == null)
                    yield break;
            }
            catch
            {
                yield break;
            }
            foreach (var fileSystemInfo in enumerator)
            {
                if (fileSystemInfo is FileInfo fileInfo)
                {
                    yield return new FileItem(string.Intern(fileInfo.Name), fileInfo.Length);
                }
                else
                if (fileSystemInfo is DirectoryInfo directoryInfo)
                {
                   if (!directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        yield return new FolderItem(string.Intern(directoryInfo.Name));
                }
            }
        }
    }
}
