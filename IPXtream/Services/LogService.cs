using System;
using System.IO;
using System.Text.RegularExpressions;

namespace IPXtream.Services;

public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPXtream", "app.log");
    private static readonly object LockObj = new();

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

            lock (LockObj)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                using var sw = File.AppendText(LogPath);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sw.WriteLine($"[{timestamp}] {message}");
                if (exceptionStr is not null)
                {
                    sw.WriteLine($"[EXCEPTION] {exceptionStr}");
                }
            }
        }
        catch
        {
            // Prevent logging failures from crashing the app
        }
    }
}

