using System.Collections.Generic;
using System.Dynamic;
using Common.Logging;
using CSVGenerator.Core.Models;
using CSVGenerator.Core.Services;

namespace CSVGenerator.App
{
    /// <summary>
    /// Application configuration wrapper for ConfigurationService
    /// </summary>
    public class AppConfig : DynamicObject, IAppConfig
    {
        private readonly IAppConfig _innerConfig = ConfigurationService.Instance.GetAppConfig();
#if NET48
        private static AppConfig _instance;
#else
        private static AppConfig? _instance;
#endif

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
#if NET48
        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }
#else
        public static AppConfig Instance => _instance ??= Load();
#endif

        /// <summary>
        /// Gets or sets the language
        /// </summary>
        public string Language
        {
            get => _innerConfig.Language;
            set => _innerConfig.Language = value;
        }

        /// <summary>
        /// Gets or sets the last BOM split file path
        /// </summary>
        public string LastBomSplitPath
        {
            get => _innerConfig.LastBomSplitPath;
            set => _innerConfig.LastBomSplitPath = value;
        }

        /// <summary>
        /// Gets or sets the last CAD pins file path
        /// </summary>
        public string LastCadPinsPath
        {
            get => _innerConfig.LastCadPinsPath;
            set => _innerConfig.LastCadPinsPath = value;
        }

        /// <summary>
        /// Gets or sets the client list
        /// </summary>
        public List<string> ClientList
        {
            get => _innerConfig.ClientList;
            set => _innerConfig.ClientList = value;
        }

        /// <summary>
        /// Gets or sets the program history
        /// </summary>
        public List<string> ProgramHistory
        {
            get => _innerConfig.ProgramHistory;
            set => _innerConfig.ProgramHistory = value;
        }

        /// <summary>
        /// Gets the path to the settings file.
        /// Delegates to the inner configuration object.
        /// </summary>
        public string SettingsPath
        {
            get => _innerConfig.SettingsPath;
            // Assuming IAppConfig only has a getter for SettingsPath.
            // If it requires a setter, uncomment the line below:
            // set => _innerConfig.SettingsPath = value;
        }

        /// <summary>
        /// Loads the configuration
        /// </summary>
        /// <param name="validateOnly">Whether to only validate without updating</param>
        /// <returns>The loaded configuration</returns>
        public static AppConfig Load(bool validateOnly = false)
        {
            Logger.Instance.LogInfo("Loading application configuration...", consoleOnly: true);
            var config = new AppConfig();

            if (!validateOnly)
            {
                ConfigurationService.Instance.UpdateAppConfig(config._innerConfig);
            }

            Logger.Instance.LogInfo($"Configuration loaded: Language={config.Language}", consoleOnly: true);
            return config;
        }

        /// <summary>
        /// Saves the configuration
        /// </summary>
        public void Save()
        {
            try
            {
                _innerConfig.Save();
                Logger.Instance.LogInfo("Configuration saved", consoleOnly: true);
            }
            catch (System.Exception ex)
            {
                Logger.Instance.LogWarning($"Error saving configuration: {ex.Message}", consoleOnly: true);
            }
        }
    }
}