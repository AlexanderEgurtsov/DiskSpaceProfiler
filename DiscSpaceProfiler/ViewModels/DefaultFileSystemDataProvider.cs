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

        public IEnumerable<string> GetDirectoriesWithSimLynksCheck(string path)
        {
            IEnumerable<DirectoryInfo> enumerator = null;
            try
            {
                var dirInfo = new DirectoryInfo(path);
                if (dirInfo == null)
                    yield break;
                enumerator = dirInfo.EnumerateDirectories();
                if (enumerator == null)
                    yield break;
            }
            catch
            {
                yield break;
            }
            foreach (var directoryInfo in enumerator)
            {
                if (!directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    yield return directoryInfo.FullName;
            }
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            IEnumerable<string> enumerator = null;
            try
            {
                enumerator = Directory.EnumerateDirectories(path);
                if (enumerator == null)
                    yield break;
            }
            catch
            {
                yield break;
            }
            foreach (var directoryInfo in enumerator)
            {
                yield return directoryInfo;
            }
        }
        public Tuple<string, long> GetFileInfo(string path)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(path);
                return new Tuple<string, long>(fileInfo.FullName, fileInfo.Length);
            }
            catch
            {
                return null;
            }
             
        }
        public IEnumerable<Tuple<string, long>> GetFiles(string path)
        {
            IEnumerable<FileInfo> enumerator = null;
            try
            {
                var dirInfo = new DirectoryInfo(path);
                if (dirInfo == null)
                    yield break;
                enumerator = dirInfo.EnumerateFiles();
                if (enumerator == null)
                    yield break;
            }
            catch
            {
                yield break;
            }
            
            foreach (var fileInfo in enumerator)
            {
                yield return new Tuple<string, long>(fileInfo.FullName, fileInfo.Length);
            }
        }

        public IEnumerable<Tuple<string, long>> GetDrives()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                if (drives == null || drives.Length == 0)
                    return Enumerable.Empty<Tuple<string, long>>();
                var result = new List<Tuple<string, long>>();
                foreach (DriveInfo driveInfo in drives)
                {
                    if (driveInfo.IsReady && driveInfo.DriveType == DriveType.Fixed)
                        result.Add(new Tuple<string, long>(driveInfo.RootDirectory.FullName, driveInfo.TotalSize - driveInfo.TotalFreeSpace));
                }
                return result;
            }
            catch
            {
                return Enumerable.Empty<Tuple<string, long>>();
            }
        }
    }
}
