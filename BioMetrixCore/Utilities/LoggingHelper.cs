using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;

namespace Native_BioReader.Utilities
{
    public static class LoggingHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static LoggingHelper()
        {
            ConfigureNLog();
        }

        public static void ConfigureNLog()
        {
            if (LogManager.Configuration == null)
            {
                var config = new LoggingConfiguration();

                // File target
                var fileTarget = new FileTarget("fileTarget")
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "${shortdate}.log"),
                    Layout = "${longdate} | ${level:uppercase=true} | ${message} ${exception:format=ToString}",
                    ArchiveEvery = FileArchivePeriod.Day,
                    MaxArchiveFiles = 7
                };

                // Console target
                var consoleTarget = new ConsoleTarget("consoleTarget")
                {
                    Layout = "${time} | ${level:uppercase=true} | ${message}"
                };

                // Add targets
                config.AddTarget(fileTarget);
                config.AddTarget(consoleTarget);

                // Define rules
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
                config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);

                // Apply configuration
                LogManager.Configuration = config;

                EnsureLogDirectoryExists();
            }
        }

        private static void EnsureLogDirectoryExists()
        {
            try
            {
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create logs directory: {ex.Message}");
            }
        }

        public static void Information(string message)
        {
            Logger.Info(message);
        }
        public static void Info(string message)
        {
            Logger.Info(message);
        }

        public static void Warning(string message, Exception ex = null)
        {
            Logger.Warn(ex, message);
        }
        public static void Warn(string message, Exception ex = null)
        {
            Logger.Warn(ex, message);
        }

        public static void Error(string message, Exception ex = null)
        {
            Logger.Error(ex, message);
        }

        public static void Debug(string message)
        {
            Logger.Debug(message);
        }

        public static void Trace(string message)
        {
            Logger.Trace(message);
        }
    }
}