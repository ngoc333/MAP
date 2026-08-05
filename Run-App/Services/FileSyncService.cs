using System;
using System.IO;

namespace RunApp;

public class SyncResult
{
    public int TotalFiles { get; set; }
    public int CopiedFiles { get; set; }
    public int SkippedFiles { get; set; }
}

public class FileSyncService
{
    private readonly string _serverPath;
    private readonly string _localPath;

    public event Action<string, int, int, int> ProgressChanged;
    public event Action<string, int, int, int> FileSkipped;

    public FileSyncService(string serverPath, string localPath)
    {
        _serverPath = serverPath;
        _localPath = localPath;
    }

    public SyncResult SyncAsync()
    {
        if (!Directory.Exists(_serverPath))
            throw new DirectoryNotFoundException($"Server path not found: {_serverPath}");

        if (!Directory.Exists(_localPath))
            Directory.CreateDirectory(_localPath);

        var serverFiles = Directory.GetFiles(_serverPath, "*", SearchOption.AllDirectories);
        var totalFiles = serverFiles.Length;
        var result = new SyncResult { TotalFiles = totalFiles };

        for (int i = 0; i < totalFiles; i++)
        {
            var serverFile = serverFiles[i];
            var relativePath = serverFile.Substring(_serverPath.Length).TrimStart(Path.DirectorySeparatorChar);
            var localFile = Path.Combine(_localPath, relativePath);
            var current = i + 1;
            var percent = (int)((double)current / totalFiles * 100);

            try
            {
                var serverInfo = new FileInfo(serverFile);
                var localInfo = new FileInfo(localFile);

                bool needCopy = !localInfo.Exists ||
                                localInfo.LastWriteTimeUtc != serverInfo.LastWriteTimeUtc;

                if (needCopy)
                {
                    var localDir = Path.GetDirectoryName(localFile);
                    if (!Directory.Exists(localDir))
                        Directory.CreateDirectory(localDir);

                    File.Copy(serverFile, localFile, true);
                    File.SetLastWriteTimeUtc(localFile, serverInfo.LastWriteTimeUtc);

                    result.CopiedFiles++;
                    ProgressChanged?.Invoke(relativePath, percent, current, totalFiles);
                }
                else
                {
                    result.SkippedFiles++;
                    FileSkipped?.Invoke(relativePath, percent, current, totalFiles);
                }
            }
            catch (Exception ex)
            {
                result.SkippedFiles++;
                FileSkipped?.Invoke($"{relativePath} ({ex.Message})", (int)((double)current / totalFiles * 100), current, totalFiles);
            }
        }

        return result;
    }
}
