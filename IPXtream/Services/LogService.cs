using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace IPXtream.Services;

public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPXtream", "app.log");

    private static readonly BlockingCollection<string> LogQueue = new();
    private static readonly Thread WriterThread;

    static LogService()
    {
        WriterThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "IPXtream-LogWriter"
        };
        WriterThread.Start();
    }

    private static void ProcessQueue()
    {
        foreach (var logLine in LogQueue.GetConsumingEnumerable())
        {
            WriteLogToFile(logLine);
        }
    }

    private static void WriteLogToFile(string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Check for log rotation (5MB = 5 * 1024 * 1024 bytes)
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 5 * 1024 * 1024)
            {
                RotateLogs();
            }

            using var sw = File.AppendText(LogPath);
            sw.WriteLine(line);
        }
        catch
        {
            // Fail silently to prevent crashing
        }
    }

    private static void RotateLogs()
    {
        try
        {
            for (int i = 3; i >= 1; i--)
            {
                var currentPath = i == 1 ? LogPath : $"{LogPath}.{i - 1}";
                var nextPath = $"{LogPath}.{i}";
                if (File.Exists(currentPath))
                {
                    if (File.Exists(nextPath))
                    {
                        File.Delete(nextPath);
                    }
                    File.Move(currentPath, nextPath);
                }
            }
        }
        catch
        {
            // Fail silently
        }
    }

    public static string RedactUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        try
        {
            // 1. Query params username=... and password=...
            var result = Regex.Replace(url, @"([?&])username=[^&]*", "$1username=[REDACTED]", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"([?&])password=[^&]*", "$1password=[REDACTED]", RegexOptions.IgnoreCase);
            
            // 2. Path segments: /live/user/pass/id or /movie/user/pass/id or /series/user/pass/id
            result = Regex.Replace(result, @"/(live|movie|series)/([^/]+)/([^/]+)/", "/$1/[REDACTED]/[REDACTED]/", RegexOptions.IgnoreCase);
            
            return result;
        }
        catch
        {
            return url;
        }
    }

    public static void Log(string message, Exception? ex = null)
    {
        try
        {
            message = RedactUrl(message);
            var exceptionStr = ex != null ? RedactUrl(ex.ToString()) : null;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            LogQueue.Add($"[{timestamp}] {message}");
            if (exceptionStr is not null)
            {
                LogQueue.Add($"[EXCEPTION] {exceptionStr}");
            }
        }
        catch
        {
            // Prevent logging failures from crashing the app
        }
    }
}

