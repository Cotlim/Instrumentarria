using System;
using System.IO;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace Instrumentarria.Helpers
{
    public static class Log
    {
        // Helper for time to log a message once every x seconds
        private static DateTime lastLogTime = DateTime.UtcNow;

        /// <summary>
        /// Log a message once every x second(s)
        /// </summary>
        public static void SlowInfo(string message, int seconds = 1, [CallerFilePath] string callerFilePath = "")
        {
            // Extract the class name from the caller's file path.
            string className = Path.GetFileNameWithoutExtension(callerFilePath);
            var instance = ModInstance;
            if (instance == null || instance.Logger == null)
                return; // Skip logging if the mod is unloading or null

            // Use TimeSpanFactory to create a x-second interval.
            TimeSpan interval = TimeSpan.FromSeconds(seconds);
            bool timeElapsed = DateTime.UtcNow - lastLogTime >= interval;
            if (timeElapsed)
            {
                // Prepend the class name to the log message.
                instance.Logger.Info($"[{className}] {message}");
                lastLogTime = DateTime.UtcNow;
            }
        }

        public static void Info(string message, [CallerFilePath] string callerFilePath = "")
        {
            // Extract the class name from the caller's file path.
            string className = Path.GetFileNameWithoutExtension(callerFilePath);
            var instance = ModInstance;
            if (instance == null || instance.Logger == null)
                return;

            // Prepend the class name to the log message.
            instance.Logger.Info($"[{className}] {message}");
        }

        public static void Warn(string message)
        {
            var instance = ModInstance;
            if (instance == null || instance.Logger == null)
                return;

            instance.Logger.Warn(message);
        }

        public static void Error(string message)
        {
            var instance = ModInstance;
            if (instance == null || instance.Logger == null)
                return;

            instance.Logger.Error(message);
        }

        private static Mod ModInstance
        {
            get
            {
                try
                {
                    return ModLoader.GetMod("Instrumentarria");
                }
                catch (Exception ex)
                {
                    Error("Error getting mod instance: " + ex.Message);
                    return null;
                }
            }
        }
    }
}