using System;
using System.Collections.Generic;

namespace CSVGenerator
{
    public class AppConfig : Common.BaseConfig
    {
        public List<string> ClientList { get; set; } = new List<string>();
        public List<string> ProgramHistory { get; set; } = new List<string>();
        public string LastBomSplitPath { get; set; } = string.Empty;
        public string LastCadPinsPath { get; set; } = string.Empty;

        /// <summary>
        /// Static constructor to initialize the configuration manager
        /// </summary>
        static AppConfig()
        {
            // Initialize the configuration manager to use LocalApplicationData with shared settings
            // Both applications will use the same settings.json file
            InitializeConfigManager(
                Common.ConfigStorageLocation.LocalApplicationData,
                "settings.json",
                "Flex",
                "FlexTools");
        }

        /// <summary>
        /// Loads the configuration from the configuration file
        /// </summary>
        /// <returns>The loaded configuration</returns>
        public static AppConfig Load()
        {
            // Create a default configuration to use if loading fails
            var defaultConfig = new AppConfig
            {
                ClientList = new List<string> { "GEC", "PBEH", "AGI", "NER", "SEA4", "SEAH", "ADVA", "NOK" },
                ProgramHistory = new List<string>(),
                LastBomSplitPath = string.Empty,
                LastCadPinsPath = string.Empty,
                Language = "Romanian"
            };

            // Load the configuration using the configuration manager
            return ConfigManager.Load(defaultConfig);
        }
    }
}
