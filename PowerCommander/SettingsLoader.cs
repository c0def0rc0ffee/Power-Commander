using System;
using System.IO;
using System.Text.Json;
using PowerCommanderSettings = PowerCommander.PowerCommanderSettings;

namespace PowerCommander
{
    public static class SettingsLoader
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static PowerCommanderSettings Load()
        {
            var defaults = new PowerCommanderSettings();

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    Save(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<PowerCommanderSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PowerCommanderSettings();

                if (settings.ShutdownTimes == null || settings.ShutdownTimes.Count == 0)
                {
                    settings.ShutdownTimes = defaults.ShutdownTimes;
                }

                return settings;
            }
            catch
            {
                return defaults;
            }
        }

        private static void Save(PowerCommanderSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}
