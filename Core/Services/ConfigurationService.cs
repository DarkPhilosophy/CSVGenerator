using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Common.Logging;
using CSVGenerator.Core.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CSVGenerator.Core.Services
{
    public class ConfigurationService
    {
        private static readonly Lazy<ConfigurationService> _instance = new Lazy<ConfigurationService>(() => new ConfigurationService());
        private readonly IConfiguration _configuration;

        public static ConfigurationService Instance => _instance.Value;
        public IConfiguration Configuration => _configuration;

        private ConfigurationService()
        {
            try
            {
                var builder = new ConfigurationBuilder();
                var config = new DefaultAppConfig();
                var settingsPath = config.SettingsPath;

                if (File.Exists(settingsPath))
                {
                    builder.AddJsonFile(settingsPath, optional: false, reloadOnChange: false);
                    Logger.Instance.LogInfo($"Configuration loaded from: {settingsPath}", consoleOnly: true);
                }
                else
                {
                    Logger.Instance.LogWarning($"No settings file at {settingsPath}, using defaults", consoleOnly: true);
                }

                _configuration = builder.Build();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "Failed to initialize configuration service", "CFG-001");
                _configuration = new ConfigurationBuilder().Build();
            }
        }

        public string GetLanguage() => _configuration["Language"] ?? _configuration["AppSettings:Language"] ?? "Romanian";

        public void UpdateAppConfig(IAppConfig config)
        {
            if (config == null) return;

            bool updated = false;

            if (string.IsNullOrEmpty(config.Language))
            {
                config.Language = GetLanguage();
                Logger.Instance.LogInfo($"Updated Language: {config.Language}", consoleOnly: true);
                updated = true;
            }

            if (updated)
            {
                config.Save();
                Logger.Instance.LogInfo("Saved updated configuration", consoleOnly: true);
            }
            else
            {
                Logger.Instance.LogInfo("No configuration updates needed", consoleOnly: true);
            }
        }

        public IAppConfig GetAppConfig()
        {
            try
            {
                var config = new DefaultAppConfig();
                Logger.Instance.LogInfo($"Final configuration: Language={config.Language}, ClientList={config.ClientList.Count}, ProgramHistory={config.ProgramHistory.Count}", consoleOnly: true);
                return config;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "Failed to get application configuration", "CFG-003");
                throw;
            }
        }

        public static void EnsureConfigFileIntegrity()
        {
            try
            {
                Logger.Instance.LogInfo("Checking configuration integrity...", consoleOnly: true);
                var config = new DefaultAppConfig();
                config.Save();
                Logger.Instance.LogInfo("Configuration integrity check passed", consoleOnly: true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "Failed to ensure configuration file integrity", "CFG-002");
            }
        }

        private class DefaultAppConfig : IAppConfig
        {
            public string Language { get; set; } = "Romanian";
            public string LastBomSplitPath { get; set; } = string.Empty;
            public string LastCadPinsPath { get; set; } = string.Empty;
            public List<string> ClientList { get; set; } = new List<string>
            {
                "GEC",
                "PBEH",
                "AGI",
                "NER",
                "SEA4",
                "SEAH",
                "ADVA",
                "NOK"
            };
            public List<string> ProgramHistory { get; set; } = new List<string>();

            private readonly string _localPath;
            private readonly string _appDataPath;
            private bool _loadedFromLocal;
            public string SettingsPath => _loadedFromLocal ? _localPath : _appDataPath;

            public DefaultAppConfig()
            {
                // Get the actual executable path instead of using AppDomain.CurrentDomain.BaseDirectory
                string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string executableDir = Path.GetDirectoryName(executablePath);

                // Use the executable directory for local settings
                _localPath = Path.Combine(executableDir, "settings.json");
                _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flex", "FlexTools", "settings.json");

                // Log the paths for debugging
                Logger.Instance.LogInfo($"Local settings path: {_localPath}", consoleOnly: true);
                Logger.Instance.LogInfo($"AppData settings path: {_appDataPath}", consoleOnly: true);

                Load();
            }

            private void Load()
            {
                if (LoadFromLocal()) return;
                if (LoadFromAppData()) return;
                Logger.Instance.LogInfo("No settings file found, using defaults", consoleOnly: true);
                Save();
            }

            private bool LoadFromLocal()
            {
                try
                {
                    if (!File.Exists(_localPath))
                    {
                        Logger.Instance.LogInfo($"No local settings file at {_localPath}", consoleOnly: true);
                        return false;
                    }

                    var json = File.ReadAllText(_localPath);
                    if (!TryParseJson(json, out var jsonObj))
                    {
                        Logger.Instance.LogWarning($"Corrupted local settings file at {_localPath}, recreating", consoleOnly: true);
                        File.Move(_localPath, _localPath + ".corrupted");
                        return false;
                    }

                    MergeSettings(jsonObj);
                    _loadedFromLocal = true;
                    Logger.Instance.LogInfo($"Loaded settings from {_localPath}", consoleOnly: true);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"Failed to load local settings: {ex.Message}", consoleOnly: true);
                    return false;
                }
            }

            private bool LoadFromAppData()
            {
                try
                {
                    if (!File.Exists(_appDataPath))
                    {
                        Logger.Instance.LogInfo($"No AppData settings file at {_appDataPath}", consoleOnly: true);
                        return false;
                    }

                    var json = File.ReadAllText(_appDataPath);
                    if (!TryParseJson(json, out var jsonObj))
                    {
                        Logger.Instance.LogWarning($"Corrupted AppData settings file at {_appDataPath}, recreating", consoleOnly: true);
                        File.Move(_appDataPath, _appDataPath + ".corrupted");
                        return false;
                    }

                    MergeSettings(jsonObj);
                    Logger.Instance.LogInfo($"Loaded settings from {_appDataPath}", consoleOnly: true);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"Failed to load AppData settings: {ex.Message}", consoleOnly: true);
                    return false;
                }
            }

            private bool TryParseJson(string json, out JObject jsonObj)
            {
                try
                {
                    jsonObj = JObject.Parse(json);
                    return true;
                }
                catch
                {
                    jsonObj = null;
                    return false;
                }
            }

            private void MergeSettings(JObject jsonObj)
            {
                Language = jsonObj["Language"]?.ToString() ?? Language;
                LastBomSplitPath = jsonObj["LastBomSplitPath"]?.ToString() ?? LastBomSplitPath;
                LastCadPinsPath = jsonObj["LastCadPinsPath"]?.ToString() ?? LastCadPinsPath;
                ClientList = jsonObj["ClientList"]?.ToObject<List<string>>() ?? ClientList;
                ProgramHistory = jsonObj["ProgramHistory"]?.ToObject<List<string>>() ?? ProgramHistory;
            }

            public void Save()
            {
                try
                {
                    var settings = new
                    {
                        Language,
                        LastBomSplitPath,
                        LastCadPinsPath,
                        ClientList,
                        ProgramHistory
                    };

                    var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                    if (_loadedFromLocal || (!_loadedFromLocal && File.Exists(_localPath)))
                    {
                        SaveTo(_localPath);
                        Logger.Instance.LogInfo($"Saved settings to {_localPath}", consoleOnly: true);
                    }
                    else
                    {
                        SaveTo(_appDataPath);
                        Logger.Instance.LogInfo($"Saved settings to {_appDataPath}", consoleOnly: true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"Failed to save settings: {ex.Message}", consoleOnly: true);
                }
            }

            private void SaveTo(string path)
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    Language,
                    LastBomSplitPath,
                    LastCadPinsPath,
                    ClientList,
                    ProgramHistory
                }, Formatting.Indented));
            }
        }
    }
}