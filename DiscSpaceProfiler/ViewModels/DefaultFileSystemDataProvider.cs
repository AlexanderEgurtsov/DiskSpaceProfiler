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

        public IEnumerable<string> GetDirectories(string path)
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch
            {
                yield break;
            }
            foreach (string directory in directories)
            {
                DirectoryInfo directoryInfo;
                try
                {
                    directoryInfo = new DirectoryInfo(directory);
                }
                catch
                {
                    continue;
                }
                if (!directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    yield return directory;
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
            string[] files;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch
            {
                yield break;
            }
            foreach (string file in files)
            {
                var info = GetFileInfo(file);
                if (info != null)
                    yield return info;
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
