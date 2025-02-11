using Newtonsoft.Json;
using Native_BioReader.Models;
using System;
using System.IO;

namespace Native_BioReader.Utilities
{
    public static class ConfigHelper
    {
        private static Config _config;

        public static Config Configuration
        {
            get
            {
                if (_config == null)
                {
                    _config = LoadConfig();
                }
                return _config;
            }
        }

        public static Config LoadConfig(string configPath = "config.json")
        {
            try
            {
                // Load base configuration
                Config config = File.Exists(configPath)
                    ? JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath))
                    : new Config();

                // Apply environment overrides
                ApplyEnvironmentOverrides(config);

                // Validate configuration
                ValidateConfig(config);

                LoggingHelper.Info("Configuration loaded successfully");
                return config;
            }
            catch (Exception ex)
            {
                LoggingHelper.Error("Failed to load configuration", ex);
                throw new ApplicationException("Configuration loading failed", ex);
            }
        }

        private static void ApplyEnvironmentOverrides(Config config)
        {
            // Base URL override
            var envBaseUrl = Environment.GetEnvironmentVariable("BIOREADER_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envBaseUrl))
            {
                config.BASE_URL = envBaseUrl;
            }
        }

        private static void ValidateConfig(Config config)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(config.BASE_URL))
                errors.Add("BASE_URL is required");

            if (errors.Count > 0)
                throw new ArgumentException($"Invalid configuration: {string.Join(", ", errors)}");
        }
    }
}