using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Xaml;
using Common.Logging;
using Common.UI.Language;
using Common.Audio;
using CSVGenerator.Core.Services;

namespace CSVGenerator.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;

                base.OnStartup(e);

                // Log application start
                Logger.Instance.LogInfo("Application starting...", true);

                // Initialize Configuration Service
                Logger.Instance.LogInfo("Initializing configuration service...", true);
                var configService = ConfigurationService.Instance;

                // Ensure configuration file integrity
                Logger.Instance.LogInfo("Ensuring configuration file integrity...", true);
                ConfigurationService.EnsureConfigFileIntegrity();

                // Initialize Common components
                Logger.Instance.LogInfo("Initializing language manager...", true);
                try
                {
                    // Get the executing assembly without logging resources
                    var mainAssembly = Assembly.GetExecutingAssembly();

                    // Initialize the language manager
                    try
                    {
                        // Initialize the language manager with the correct assembly and path
                        LanguageManager.Instance.Initialize("CSVGenerator", "App/Languages");

                        // Get the language from the configuration
                        var appConfig = ConfigurationService.Instance.GetAppConfig();
                        string language = appConfig != null && !string.IsNullOrEmpty(appConfig.Language) ? appConfig.Language : "English";

                        // Switch to the configured language
                        LanguageManager.Instance.SwitchLanguage(language);

                        Logger.Instance.LogInfo($"Language manager initialized with language: {language}", true);
                    }
                    catch (Exception langEx)
                    {
                        Logger.Instance.LogWarning($"Failed to initialize language manager: {langEx.Message}", true);
                    }

                    // Initialize the sound player
                    try
                    {
                        // Register the button click sound from embedded resources
                        string soundResourcePath = "CSVGenerator.g.resources.app.sounds.ui-minimal-click.wav";

                        // Get the executing assembly without logging resources
                        var assembly = Assembly.GetExecutingAssembly();

                        // Register the sound with the specific assembly
                        Common.Audio.SoundPlayer.RegisterSound("ButtonClick", soundResourcePath, assembly);
                        Logger.Instance.LogInfo("Sound registered successfully", true);
                    }
                    catch (Exception soundEx)
                    {
                        Logger.Instance.LogWarning($"Failed to initialize sound player: {soundEx.Message}", true);
                    }
                }
                catch (Exception langEx)
                {
                    Logger.Instance.LogException(langEx, "Failed to initialize language manager", "APP-002");
                    MessageBox.Show($"Failed to initialize language manager: {langEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Log application version
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                Logger.Instance.LogInfo($"Application version: {version}", true);

                // Log startup information
                Logger.Instance.LogInfo($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}", true);
                Logger.Instance.LogInfo($"Current directory: {Environment.CurrentDirectory}", true);

                // Get the actual executable path
                var mainModule = Process.GetCurrentProcess().MainModule;
                if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                {
                    string executablePath = mainModule.FileName;
                    Logger.Instance.LogInfo($"Executable path: {executablePath}", true);

                    // Do not create Ads directory - only use it if it already exists
                    string executableDir = Path.GetDirectoryName(executablePath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(executableDir))
                    {
                        string adsDirectory = Path.Combine(executableDir, "Ads");
                        if (Directory.Exists(adsDirectory))
                        {
                            Logger.Instance.LogInfo($"Ads directory exists at: {adsDirectory}", true);
                        }
                        else
                        {
                            Logger.Instance.LogInfo("Ads directory does not exist - will use network paths only", true);
                        }
                    }
                    else
                    {
                        Logger.Instance.LogInfo("Could not determine executable directory - will use network paths only", true);
                    }
                }
                else
                {
                    Logger.Instance.LogInfo("Could not determine executable path - will use network paths only", true);
                }
            }
            catch (Exception ex)
            {
                // Log the exception with the enhanced error handling
                Logger.Instance.LogException(ex, "Application startup", "APP-001");

                // Show error message with user-friendly information
                MessageBox.Show($"Application startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Shutdown the application
                Current.Shutdown();
            }
        }

        /// <summary>
        /// Handles unhandled exceptions in the dispatcher
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                e.Handled = true;

                // Log the exception with the enhanced error handling
                Logger.Instance.LogException(e.Exception, "Unhandled UI exception", "APP-006");

                // Show error message with user-friendly information
                MessageBox.Show($"An unhandled UI exception occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // Last resort logging if the exception handler itself fails
                System.Diagnostics.Debug.WriteLine($"Critical error in exception handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles unhandled exceptions in the current domain
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                // Log the exception with the enhanced error handling
                var exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    Logger.Instance.LogException(exception, "Unhandled application exception", "APP-006");

                    // Show error message with user-friendly information if possible
                    MessageBox.Show($"A fatal error occurred: {exception.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Logger.Instance.LogError($"Unknown error occurred: {e.ExceptionObject}", false, "APP-006");
                }

                // If the exception is terminal, shutdown the application
                if (e.IsTerminating)
                {
                    Logger.Instance.LogError("Application is terminating due to an unhandled exception", false, "APP-006");
                }
            }
            catch (Exception ex)
            {
                // Last resort logging if the exception handler itself fails
                System.Diagnostics.Debug.WriteLine($"Critical error in exception handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads language resources
        /// </summary>
        /// <param name="language">The language to load</param>
        public void LoadLanguageResources(string language)
        {
            try
            {
                // Default to English if language is null or empty
                if (string.IsNullOrEmpty(language))
                {
                    language = "English";
                }

                // Initialize the language manager with the correct assembly and path
                LanguageManager.Instance.Initialize("CSVGenerator", "App/Languages");

                // Switch to the configured language
                LanguageManager.Instance.SwitchLanguage(language);

                // Register the button click sound
                Common.Audio.SoundPlayer.RegisterSound("ButtonClick", "CSVGenerator.g.resources.app.sounds.ui-minimal-click.wav", Assembly.GetExecutingAssembly());

                Logger.Instance.LogInfo($"Language resources loaded for {language}", true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex, "Failed to load language resources", "APP-004");
                MessageBox.Show($"Failed to load language resources: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
