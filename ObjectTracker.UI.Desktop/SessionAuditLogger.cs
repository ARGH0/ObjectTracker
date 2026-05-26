using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ObjectTracker.UI.Desktop;

internal sealed class SessionAuditLogger : IDisposable
{
    private readonly Lock sync = new();
    private StreamWriter? writer;

    public string? CurrentFilePath { get; private set; }

    public bool IsActive
    {
        get
        {
            lock (sync)
            {
                return writer is not null;
            }
        }
    }

    public void StartSession()
    {
        lock (sync)
        {
            StopSessionCore();

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var sessionFolder = Path.Combine(appDataPath, "ObjectTracker", "sessions");
            Directory.CreateDirectory(sessionFolder);

            var timestamp = DateTime.Now;
            var fileName = $"session-{timestamp:yyyyMMdd-HHmmss}.log";
            var filePath = Path.Combine(sessionFolder, fileName);

            writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
            CurrentFilePath = filePath;

            writer.WriteLine($"[{timestamp:O}] Session started.");
        }
    }

    public void Append(string line)
    {
        lock (sync)
        {
            writer?.WriteLine(line);
        }
    }

    public void StopSession()
    {
        lock (sync)
        {
            StopSessionCore();
        }
    }

    public void Dispose()
    {
        StopSession();
    }

    private void StopSessionCore()
    {
        if (writer is not null)
        {
            writer.WriteLine($"[{DateTime.Now:O}] Session stopped.");
            writer.Dispose();
            writer = null;
        }

        CurrentFilePath = null;
    }
}
