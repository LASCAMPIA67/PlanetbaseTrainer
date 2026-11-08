using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace PlanetbaseTrainer.Core
{
    /// <summary>Thin wrapper over the BepInEx logger with de-duplication for errors raised in per-frame code.</summary>
    internal static class Log
    {
        private static ManualLogSource _source;
        private static readonly HashSet<string> ReportedKeys = new HashSet<string>();

        /// <summary>Number of errors logged since startup (used by the self-test report).</summary>
        public static int ErrorCount { get; private set; }

        public static void Init(ManualLogSource source) => _source = source;

        public static void Info(string message) => _source?.LogInfo(message);

        public static void Warn(string message) => _source?.LogWarning(message);

        public static void Error(string message)
        {
            ErrorCount++;
            _source?.LogError(message);
        }

        /// <summary>Logs an exception only the first time a given key fails (avoids flooding the log from Update/OnGUI).</summary>
        public static void ErrorOnce(string key, Exception ex)
        {
            ErrorCount++;
            if (ReportedKeys.Add(key))
                _source?.LogError($"[{key}] {ex}");
        }

        /// <summary>
        /// Writes pending lines to BepInEx\LogOutput.log. BepInEx flushes its disk log on a timer, so without this the
        /// messages logged while the game quits would never reach the file.
        /// </summary>
        public static void Flush()
        {
            try
            {
                foreach (var listener in Logger.Listeners)
                {
                    if (listener is DiskLogListener disk)
                        disk.LogWriter?.Flush();
                }
            }
            catch (Exception)
            {
                // Best effort, at shutdown.
            }
        }
    }
}
