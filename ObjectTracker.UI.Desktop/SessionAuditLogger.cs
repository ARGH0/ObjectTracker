using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ObjectTracker.UI.Desktop;

internal sealed class SessionAuditLogger : IDisposable
{
    private readonly Lock sync = new();
    private StreamWriter? writer;

    public const string EventRunStart = "run-start";
    public const string EventRunStop = "run-stop";
    public const string EventStatus = "status";
    public const string EventCameraSwitch = "camera-switch";
    public const string EventCalibrationChange = "calibration-change";
    public const string EventAmbiguityRaised = "ambiguity-raised";
    public const string EventAmbiguityResolved = "ambiguity-resolved";

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

    public void AppendStatus(string status)
    {
        AppendEvent(EventStatus, status);
    }

    public void AppendEvent(string eventType, string message, params (string Key, string Value)[] fields)
    {
        lock (sync)
        {
            if (writer is null)
            {
                return;
            }

            var timestamp = DateTime.UtcNow.ToString("O");
            var cleanType = Sanitize(eventType);
            var cleanMessage = Sanitize(message);
            var line = $"{timestamp}|{cleanType}|{cleanMessage}";

            if (fields.Length > 0)
            {
                var metadata = string.Join(";", fields.Select(field => $"{Sanitize(field.Key)}={Sanitize(field.Value)}"));
                line = $"{line}|{metadata}";
            }

            writer.WriteLine(line);
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

    private static string Sanitize(string value)
    {
        return value
            .Replace("|", "/", StringComparison.Ordinal)
            .Replace(";", ",", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
