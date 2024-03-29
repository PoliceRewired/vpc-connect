﻿using System;
using ConnectApp.Maui.Data;
using ConnectApp.Maui.Data.Entities;
using ConnectApp.Maui.Devices;

namespace ConnectApp.Maui.AppLog
{
    public class AppLogger
    {
        private ConnectAppData db;
        public string subject;
        public bool useSystemLog;
        public bool onDevice;

#if DEBUG
        private bool mayLogSensitive = true;
#else
        private bool mayLogSensitive = false;
#endif

        private static Dictionary<LogEntry.LogLevel, char> charFor = new Dictionary<LogEntry.LogLevel, char>()
        {
            { LogEntry.LogLevel.Verbose, 'V' },
            { LogEntry.LogLevel.Debug, 'D' },
            { LogEntry.LogLevel.Info, 'I' },
            { LogEntry.LogLevel.Warning, 'W' },
            { LogEntry.LogLevel.Error, 'E' },
            { LogEntry.LogLevel.Exception, 'X' },
        };

        public AppLogger(ConnectAppData db, string subject = null, bool onDevice = true)
        {
            this.db = db;
            this.subject = subject ?? "(static)";
            this.onDevice = onDevice;
            this.useSystemLog = onDevice;
        }

        public AppLogger For(object subject)
        {
            return new AppLogger(db, subject.GetType().FullName);
        }

        public AppLogger For(string subject)
        {
            return new AppLogger(db, subject);
        }

        public AppLogger Testing(object subject)
        {
            return new AppLogger(db, subject.GetType().FullName, false);
        }

        public void RegisterForExceptions()
        {
            AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
            {
                Exception(eventArgs.Exception);
            };
        }

        public void Verbose(string m, bool s, Exception e = null) { Log(LogEntry.LogLevel.Verbose, m, s, e); }
        public void Debug(string m, bool s, Exception e = null) { Log(LogEntry.LogLevel.Debug, m, s, e); }
        public void Info(string m, bool s, Exception e = null) { Log(LogEntry.LogLevel.Info, m, s, e); }
        public void Warning(string m, bool s, Exception e = null) { Log(LogEntry.LogLevel.Warning, m, s, e); }
        public void Error(string m, bool s, Exception e = null) { Log(LogEntry.LogLevel.Error, m, s, e); }
        public void Exception(Exception e) { Log(LogEntry.LogLevel.Exception, $"{e.GetType().Name}: {e.Message}", false, e); }

        public string Version { get { return VersionTracking.CurrentVersion; } }
        public string Build { get { return VersionTracking.CurrentBuild; } }

        public string DeviceDescription { get { return ConnectDevice.DeviceDescription; } }
        public string DeviceUUID { get { return ConnectDevice.UUID; } }

        public void Log(LogEntry.LogLevel level, string message, bool isSensitive, Exception exception = null)
        {
            var log = new LogEntry()
            {
                DbVersion = DbConstants.Version,
                AppVersion = onDevice ? VersionTracking.CurrentVersion : "Test",
                AppBuild = onDevice ? VersionTracking.CurrentBuild : "Test",
                DeviceDescription = onDevice ? ConnectDevice.DeviceDescription : "Test",
                DeviceUUID = onDevice ? ConnectDevice.UUID : "Test",
                Logged = DateTime.Now,
                Subject = subject,
                Message = message,
                Level = level,
                ExceptionType = exception?.GetType().FullName,
                StackTrace = exception?.StackTrace
            };

            // log only non-sensitive material, or everything in debug mode
            if (!isSensitive || mayLogSensitive)
            {
                try
                {
                    if (db != null)
                    {
                        db.Insert(log);
                    }
                }
                catch (Exception e)
                {
                    SystemLog(LogEntry.LogLevel.Error, "Error storing the following log entry.", e);
                }
                finally
                {
                    SystemLog(level, message, exception);
                }
            }
        }

        private void SystemLog(LogEntry.LogLevel level, string message, Exception exception = null)
        {
            if (useSystemLog)
            {
                var exceptionStr = exception == null ? "" : $"; {exception}";
                Console.WriteLine($"[{charFor[level]}] {subject}: {message}{exceptionStr}");
            }
        }

    }
}
