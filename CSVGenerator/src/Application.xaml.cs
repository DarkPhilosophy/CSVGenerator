using System;
using System.Windows;
using Common;

namespace CSVGenerator
{
    /// <summary>
    /// Interaction logic for Application.xaml
    /// </summary>
    public partial class Application : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize Common components
            Common.LanguageManager.Instance.Initialize("CSVGenerator");

            // Register common sounds
            Common.SoundPlayer.RegisterCommonSounds("assets/Sounds");

            // Log application start
            Common.Logger.Instance.LogInfo("Application started");
        }
    }
}
