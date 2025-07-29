using System;
using System.IO;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace Instrumentarria.Helpers
{
    public static class Log
    {
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