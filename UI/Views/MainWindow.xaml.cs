using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Text.RegularExpressions; // For Regex, RegexOptions, Match
using System.Windows.Documents;
using System.Windows.Forms; // For clipboard operations
using Common.Logging;
using Common.UI;
using Common.UI.Animation;
using Common.Audio;
using Common.Update;
using CSVGenerator.Core.Services;
using CSVGenerator.Core.Models;

namespace CSVGenerator.UI.Views
{
    public partial class MainWindow : Window
    {
        private readonly IAppConfig _config = ConfigurationService.Instance.GetAppConfig();
#if NET48
        private Window _logWindow;
        private ScrollViewer _logScrollViewer;
        private System.Windows.Controls.TextBox _logWindowTextBox;
#else
        private Window? _logWindow;
        private ScrollViewer? _logScrollViewer;
        private System.Windows.Controls.TextBox? _logWindowTextBox;
#endif
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private bool _isLogWindowAttached = true;
        private System.Windows.Point _lastMainWindowPosition;
        private bool _loggerSubscribed = false;

        private readonly Dictionary<UIElement, object> _activeAnimations = new Dictionary<UIElement, object>();
        private static readonly SolidColorBrush BlueColor = AnimationManager.BlueColor;
        private static readonly SolidColorBrush RedColor = AnimationManager.RedColor;
        private static readonly SolidColorBrush YellowColor = AnimationManager.YellowColor;
        private static readonly SolidColorBrush GrayColor = AnimationManager.GrayColor;
        private static readonly SolidColorBrush GreenColor = AnimationManager.GreenColor;

        private static readonly Dictionary<string, double> ConversionFactors = new Dictionary<string, double>
        {
            { "mm", 1.0 },
            { "inch", 25.4 }
        };

#if NET48
        private AutoUpdater _autoUpdater;
#else
        private AutoUpdater? _autoUpdater;
#endif
#if NET48
        private Version _currentVersion = new Version(1, 0, 1, 0);
        private Common.Update.UpdateInfo _latestUpdateInfo; // Store the latest update info
#else
        private Version _currentVersion = new Version(1, 0, 1, 0);
        private Common.Update.UpdateInfo? _latestUpdateInfo; // Store the latest update info
#endif

        public MainWindow()
        {
            try
            {
                Logger.Instance.LogInfo("Initializing MainWindow...", true);
                InitializeComponent();
                Loaded += MainWindow_Loaded;

                Logger.Instance.LogInfo("Initializing application...", true);
                InitializeApplication();
                Logger.Instance.LogInfo("Application initialized successfully", true);

                Logger.Instance.LogInfo("Initializing auto-updater...", true);
                Version assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? _currentVersion;
                if (assemblyVersion.ToString() != "0.0.0.0")
                {
                    _currentVersion = assemblyVersion;
                }

                Logger.Instance.LogInfo($"Application version detected: {_currentVersion}", true);
                _autoUpdater = new AutoUpdater("CSVGenerator", _currentVersion, consoleOnly: true)
                    .AddGitHubSource("DarkPhilosophy", "CSVGenerator", consoleOnly: true);
                Logger.Instance.LogInfo($"Auto-updater initialized for version {_currentVersion}", true);

                Logger.Instance.LogInfo("MainWindow constructor completed successfully", true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in MainWindow constructor: {ex.Message}");
                Logger.Instance.LogError($"Stack trace: {ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error initializing MainWindow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Instance.LogError($"Error initializing MainWindow: {ex.Message}", consoleOnly: false, errorCode: "UI-001");
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void InitializeApplication()
        {
            try
            {
                FileParser.Instance.Initialize(LogMessage);
                LogMessage("Initializing language manager...", consoleOnly: true);
                Common.UI.Language.LanguageManager.Instance.Initialize("CSVGenerator", "App/Languages");
                LogMessage($"Setting language to {_config.Language}", consoleOnly: true);
                Common.UI.Language.LanguageManager.Instance.SwitchLanguage(_config.Language);
            }
            catch (Exception ex)
            {
                LogMessage($"Error in InitializeApplication: {ex.Message}", isError: true);
                throw;
            }

            foreach (var client in _config.ClientList)
            {
                cmbClient.Items.Add(client);
            }
            if (cmbClient.Items.Count > 0)
            {
                cmbClient.SelectedIndex = 0;
            }

            foreach (var program in _config.ProgramHistory)
            {
                cmbProgram.Items.Add(program);
            }
            if (cmbProgram.Items.Count > 0)
            {
                cmbProgram.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(_config.LastBomSplitPath) && File.Exists(_config.LastBomSplitPath))
            {
                txtBomSplitPath.Text = _config.LastBomSplitPath;
                string unit = FileParser.Instance.DetectUnit(_config.LastBomSplitPath);
                btnBomSplitUnit.Content = unit;
            }

            if (!string.IsNullOrEmpty(_config.LastCadPinsPath) && File.Exists(_config.LastCadPinsPath))
            {
                txtCadPinsPath.Text = _config.LastCadPinsPath;
                string unit = FileParser.Instance.DetectUnit(_config.LastCadPinsPath);
                btnCadPinsUnit.Content = unit;
            }

            LogMessage("Application started", consoleOnly: true);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Instance.LogInfo("MainWindow_Loaded started...", consoleOnly: true);
                this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
                this.Top = 20;

                try
                {
                    var assembly = Assembly.GetEntryAssembly();
                    string resourceName = _config.Language == "English"
                        ? "CSVGenerator.g.resources.app.images.united-states.png"
                        : "CSVGenerator.g.resources.app.images.romania.png";

                    using (var stream = assembly?.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            imgLanguageFlag.Source = bitmap;
                            Logger.Instance.LogInfo($"Loaded flag image from embedded resource: {resourceName}", consoleOnly: true);
                        }
                        else
                        {
                            Logger.Instance.LogWarning($"Could not find embedded resource: {resourceName}", consoleOnly: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning($"Could not load flag image: {ex.Message}", consoleOnly: true);
                }

                LoadImagesFromEmbeddedResources();

                var txtAdBanner = FindName("txtAdBanner") as TextBlock;
                var adContainer = FindName("adContainer") as Grid;
                var adBannerContainer = FindName("adBannerContainer") as Border;

                if (txtAdBanner != null && adContainer != null && adBannerContainer != null)
                {
                    LogMessage("Using network paths for ads, local paths only as fallback if exist", consoleOnly: true);

                    // Load and display ads
                    await LoadAndDisplayAds(txtAdBanner, adContainer, adBannerContainer);
                }

                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
                CreateLogWindow();
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
                LogMessage("MainWindow_Loaded completed successfully", consoleOnly: true);

                string welcomeMessage = FindResource("ReadyToProcess")?.ToString() ?? "Ready to process files. Click one of the buttons to begin.";
                LogMessage(welcomeMessage, isInfo: true);

                await Task.Delay(3000);
                if (_autoUpdater != null)
                {
                    await CheckForUpdatesAsync(userInitiated: false);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error in MainWindow_Loaded: {ex.Message}");
                Logger.Instance.LogError($"Stack trace: {ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error loading application: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSelectBomSplit_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnSelectBomSplit_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = FindResource("OpenFileDialogTitle") as string ?? "Select File",
                Filter = FindResource("FileFilter") as string ?? "All Files (*.*)|*.*",
                Multiselect = false
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                txtBomSplitPath.Text = openFileDialog.FileName;
                _config.LastBomSplitPath = openFileDialog.FileName;
                _config.Save();
                string unit = FileParser.Instance.DetectUnit(openFileDialog.FileName);
                btnBomSplitUnit.Content = unit;
                LogMessage($"BomSplit file selected: {Path.GetFileName(openFileDialog.FileName)}, unit: {unit}");
            }
            else
            {
                // Clear the text box and saved path when no file is selected
                txtBomSplitPath.Text = FindResource("DropFileHere")?.ToString() ?? "Drop file here";
                _config.LastBomSplitPath = string.Empty;
                _config.Save();
                LogMessage("BomSplit file selection cancelled, cleared saved path", consoleOnly: true);
            }
        }

        private void BtnSelectCadPins_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnSelectCadPins_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = FindResource("OpenFileDialogTitle") as string ?? "Select File",
                Filter = FindResource("FileFilter") as string ?? "All Files (*.*)|*.*",
                Multiselect = false
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                txtCadPinsPath.Text = openFileDialog.FileName;
                _config.LastCadPinsPath = openFileDialog.FileName;
                _config.Save();
                string unit = FileParser.Instance.DetectUnit(openFileDialog.FileName);
                btnCadPinsUnit.Content = unit;
                LogMessage($"CadPins file selected: {Path.GetFileName(openFileDialog.FileName)}, unit: {unit}");
            }
            else
            {
                // Clear the text box and saved path when no file is selected
                txtCadPinsPath.Text = FindResource("DropFileHere")?.ToString() ?? "Drop file here";
                _config.LastCadPinsPath = string.Empty;
                _config.Save();
                LogMessage("CadPins file selection cancelled, cleared saved path", consoleOnly: true);
            }
        }

        private void CmbClient_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isClientBeingDeleted)
            {
                return;
            }

            if (e.OriginalSource is System.Windows.Controls.ComboBox && Mouse.DirectlyOver is System.Windows.Controls.Button button &&
                button.Name == "ClientDeleteButton")
            {
                return;
            }

            string text = cmbClient.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (_clientBeingDeleted != null && string.Equals(text, _clientBeingDeleted, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                bool exists = false;
                foreach (string item in cmbClient.Items)
                {
                    if (string.Equals(item, text, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    cmbClient.Items.Add(text);
                    cmbClient.SelectedItem = text;
                    _config.ClientList.Add(text);
                    _config.Save();
                    LogMessage($"Added new client: {text}");
                }
            }
        }

        private bool _isClientBeingDeleted = false;
#if NET48
        private string _clientBeingDeleted = null;
#else
        private string? _clientBeingDeleted = null;
#endif
        private bool _isProgramBeingDeleted = false;
#if NET48
        private string _programBeingDeleted = null;
#else
        private string? _programBeingDeleted = null;
#endif

        private void ClientDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is string clientName)
                {
                    _isClientBeingDeleted = true;
                    _clientBeingDeleted = clientName;
                    string currentText = cmbClient.Text;
                    cmbClient.Items.Remove(clientName);

                    if (_config.ClientList.Contains(clientName))
                    {
                        _config.ClientList.Remove(clientName);
                        _config.Save();
                    }

                    cmbClient.SelectedIndex = -1;

                    if (!string.Equals(currentText, clientName, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(currentText))
                    {
                        cmbClient.Text = currentText;
                    }
                    else if (cmbClient.Items.Count > 0)
                    {
                        cmbClient.SelectedIndex = 0;
                    }
                    else
                    {
                        cmbClient.Text = string.Empty;
                    }

                    LogMessage($"Removed client: {clientName}");
                    e.Handled = true;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isClientBeingDeleted = false;
                        _clientBeingDeleted = null;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error removing client: {ex.Message}");
                e.Handled = true;
                _isClientBeingDeleted = false;
                _clientBeingDeleted = null;
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnGenerate_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            string dropFileHere = FindResource("DropFileHere")?.ToString() ?? "Drop file here";
            if (string.IsNullOrEmpty(txtBomSplitPath.Text) || txtBomSplitPath.Text == dropFileHere || !File.Exists(txtBomSplitPath.Text))
            {
                string errorMessage = FindResource("SelectBomSplitFirst")?.ToString() ?? "Please select a BomSplit file first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                System.Windows.MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbClient.SelectedItem == null)
            {
                string errorMessage = FindResource("SelectClientFirst")?.ToString() ?? "Please select a client first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                System.Windows.MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(cmbProgram.Text))
            {
                string errorMessage = FindResource("EnterProgramFirst")?.ToString() ?? "Please enter a program first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                System.Windows.MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerateCSVFiles();
        }

        private void GenerateCSVFiles()
        {
            try
            {
                IsEnabled = false;
                Cursor = System.Windows.Input.Cursors.Wait;

                string processingMessage = FindResource("Processing")?.ToString() ?? "Processing...";
                progressBar.Value = 0;
                txtProgress.Text = processingMessage;

                string bomFilePath = txtBomSplitPath.Text;
                string pinsFilePath = txtCadPinsPath.Text;
                string client = cmbClient.SelectedItem?.ToString() ?? "UNKNOWN_CLIENT";
                string program = cmbProgram.Text;

                string bomUnit = btnBomSplitUnit.Content?.ToString()?.ToLower() ?? "mm";
                string pinsUnit = btnCadPinsUnit.Content?.ToString()?.ToLower() ?? "mm";
                double bomFactor = ConversionFactors.ContainsKey(bomUnit) ? ConversionFactors[bomUnit] : 1.0;
                double pinsFactor = ConversionFactors.ContainsKey(pinsUnit) ? ConversionFactors[pinsUnit] : 1.0;

                if (_config.ProgramHistory.Contains(program))
                {
                    _config.ProgramHistory.Remove(program);
                }

                _config.ProgramHistory.Insert(0, program);
                if (_config.ProgramHistory.Count > 10)
                {
                    _config.ProgramHistory.RemoveAt(_config.ProgramHistory.Count - 1);
                }
                _config.Save();

                bool isProgramInItems = false;
                foreach (var item in cmbProgram.Items)
                {
                    if (string.Equals(item.ToString(), program, StringComparison.OrdinalIgnoreCase))
                    {
                        isProgramInItems = true;
                        break;
                    }
                }

                if (!isProgramInItems)
                {
                    cmbProgram.Items.Add(program);
                }

                cmbProgram.SelectedItem = program;

                System.Windows.Media.Color vibrantGreen = System.Windows.Media.Color.FromRgb(0, 200, 0);
                var heartbeatConfigs = AnimationManager.Instance.CreateAnimationPreset("heartbeat");
                var glowConfig = new AnimationManager.AnimationConfig
                {
                    Type = AnimationManager.AnimationType.Glow,
                    TargetBrush = new SolidColorBrush(vibrantGreen),
                    From = 20,
                    To = 40,
                    DurationSeconds = 0.7,
                    Priority = 3,
                    Easing = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                btnGenerate.Tag = "GreenButton";
                var allConfigs = new List<AnimationManager.AnimationConfig>(heartbeatConfigs) { glowConfig };
                AnimationManager.Instance.ApplyAnimations(btnGenerate, allConfigs.ToArray());

                string parsingBomMessage = FindResource("ParsingBomFile")?.ToString() ?? "Parsing BOM file...";
                UpdateProgress(10, parsingBomMessage);
                var bomData = FileParser.Instance.ParseBomFile(bomFilePath);
                if (bomData.Count == 0)
                {
                    throw new Exception("Failed to parse BOM file or no valid data found.");
                }
                string parsedBomMsg = string.Format(FindResource("ParsedBomFile")?.ToString() ?? "Parsed BOM file with {0} parts", bomData.Count);
                LogMessage(parsedBomMsg, isInfo: true);

#if NET48
                List<PinsData> pinsData = null;
#else
                List<PinsData>? pinsData = null;
#endif
                string dropFileHere = FindResource("DropFileHere")?.ToString() ?? "Drop file here";
                if (!string.IsNullOrEmpty(pinsFilePath) && pinsFilePath != dropFileHere && File.Exists(pinsFilePath))
                {
                    string parsingPinsMessage = FindResource("ParsingPinsFile")?.ToString() ?? "Parsing PINS file...";
                    UpdateProgress(30, parsingPinsMessage);
                    pinsData = FileParser.Instance.ParsePinsFile(pinsFilePath);
                    if (pinsData != null)
                    {
                        string parsedPinsMsg = string.Format(FindResource("ParsedPinsFile")?.ToString() ?? "Parsed PINS file with {0} parts", pinsData.Count);
                        LogMessage(parsedPinsMsg, isInfo: true);
                    }
                }

                Action<int, string> customProgressCallback = (progress, message) =>
                {
                    progressBar.Value = progress;
                    txtProgress.Text = message;
                    progressBar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                };

                var result = GenCSV.Instance.GenerateCSVFiles(
                    bomData,
                    pinsData,
                    client,
                    program,
                    bomFactor,
                    pinsFactor,
                    customProgressCallback,
                    (message, isError, isInfo, isSuccess, isWarning, consoleOnly) => LogMessage(message, isError, isWarning, isSuccess, isInfo, consoleOnly));

                if (result["top"] || result["bot"])
                {
                    string completedMessage = FindResource("ProcessingCompleted")?.ToString() ?? "Processing completed.";
                    progressBar.Value = 100;
                    txtProgress.Text = completedMessage;
                    progressBar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                    string completedTitle = FindResource("Completed")?.ToString() ?? "Completed";
                    System.Windows.MessageBox.Show(completedMessage, completedTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    SoundPlayer.PlaySound("ButtonClick");
                }
                else
                {
                    throw new Exception("No CSV files were generated.");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = FindResource("Error")?.ToString() ?? "Error";
                string processingFailedMessage = FindResource("ProcessingFailed")?.ToString() ?? "Processing failed.";
                UpdateProgress(0, errorMessage);
                System.Windows.MessageBox.Show($"{processingFailedMessage}\n\n{ex.Message}", errorMessage, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                Cursor = System.Windows.Input.Cursors.Arrow;
                AnimationManager.Instance.StopAnimations(btnGenerate);
                btnGenerate.Tag = null;
                btnGenerate.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
                btnGenerate.RenderTransform = new ScaleTransform(1.0, 1.0);
                var defaultEffect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 15,
                    Opacity = 0.7
                };
                btnGenerate.Effect = defaultEffect;
                btnGenerate.InvalidateVisual();
            }
        }

        private void UpdateProgress(int value, string message)
        {
            progressBar.Value = value;
            txtProgress.Text = message;
            if (value == 0 || value == 10 || value == 30)
            {
                LogMessage(message);
            }
            progressBar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        public void LogMessage(string message, bool isError = false, bool isWarning = false, bool isSuccess = false, bool isInfo = false, bool consoleOnly = false)
        {
            Logger.Instance.LogMessage(message, isError, isWarning, isSuccess, isInfo, consoleOnly);

            if (!_loggerSubscribed)
            {
                Logger.Instance.OnLogMessage += (formattedMessage, error, warning, success, info, console, data) =>
                {
                    if (!console)
                    {
                        _logBuffer.Append(formattedMessage + Environment.NewLine);
                        string bufferText = _logBuffer.ToString();
                        string[] lines = bufferText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 100)
                        {
                            _logBuffer.Clear();
                            _logBuffer.Append(string.Join(Environment.NewLine, lines.Skip(lines.Length - 100)) + Environment.NewLine);
                        }

                        if (_logWindow != null && _logWindow.IsVisible && _logWindowTextBox != null)
                        {
                            _logWindowTextBox.AppendText(formattedMessage + Environment.NewLine);
                            _logWindowTextBox.ScrollToEnd();
                        }
                    }
                };
                _loggerSubscribed = true;
            }
        }

        private void CreateLogWindow()
        {
            _logWindow = new Window
            {
                Title = FindResource("LogWindowTitle")?.ToString() ?? "Log Window",
                Width = 600,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                Owner = this
                // MinHeight = 200,
                // MinWidth = 300
            };

            _logWindow.Left = this.Left + this.Width;
            _logWindow.Top = this.Top;
            _lastMainWindowPosition = new System.Windows.Point(this.Left, this.Top);

            // ScrollViewer
            _logScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, // Let ScrollViewer handle HScroll
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,   // Let ScrollViewer handle VScroll
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Padding = new Thickness(10) // Apply padding here for space around TextBox
            };

            _logWindowTextBox = new System.Windows.Controls.TextBox
            {
                //VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                //HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,         // Needed for horizontal scrolling
                IsReadOnly = true,
                BorderThickness = new Thickness(0),         // No border on TextBox itself
                Background = System.Windows.Media.Brushes.Transparent, // Transparent background
                VerticalAlignment = VerticalAlignment.Top,  // Align text content to top within ScrollViewer
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch, // Stretch horizontally within ScrollViewer
                FontFamily = new System.Windows.Media.FontFamily("Consolas"), // Monospace font for better alignment
                FontSize = 14, // Text size
                IsUndoEnabled = false, // Disable undo
                AcceptsReturn = true, // Allow line breaks
                AcceptsTab = true // Allow tab characters
            };

            // --- Context Menu for TextBox ---
            System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem copyAllMenuItem = new System.Windows.Controls.MenuItem { Header = FindResource("CopyAll")?.ToString() ?? "Copy All" };
            copyAllMenuItem.Click += (s, args) =>
            {
                try
                {
                    if (_logWindowTextBox != null && !string.IsNullOrEmpty(_logWindowTextBox.Text))
                    {
                        string errorMessage;
                        if (SafeCopyToClipboard(_logWindowTextBox.Text, out errorMessage))
                        {
                            LogMessage(FindResource("CopiedToClipboard")?.ToString() ?? "Text copied to clipboard", isSuccess: true);
                        }
                        else if (!string.IsNullOrEmpty(errorMessage))
                        {
                            string waitMessage = FindResource("WaitBeforeCopying")?.ToString() ?? "Please wait before copying again";
                            if (errorMessage != waitMessage)
                            {
                                LogMessage($"{FindResource("ClipboardError")?.ToString() ?? "Error copying to clipboard"}: {errorMessage}", isError: true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"{FindResource("ClipboardError")?.ToString() ?? "Error copying to clipboard"}: {ex.Message}", isError: true);
                }
            };
            contextMenu.Items.Add(copyAllMenuItem);

            System.Windows.Controls.MenuItem copySelectedMenuItem = new System.Windows.Controls.MenuItem { Header = FindResource("CopySelected")?.ToString() ?? "Copy Selected" };
            copySelectedMenuItem.Click += (s, args) =>
            {
                try
                {
                    if (_logWindowTextBox != null && !string.IsNullOrEmpty(_logWindowTextBox.SelectedText))
                    {
                        string errorMessage;
                        if (SafeCopyToClipboard(_logWindowTextBox.SelectedText, out errorMessage))
                        {
                            LogMessage(FindResource("SelectionCopiedToClipboard")?.ToString() ?? "Selection copied to clipboard", isSuccess: true);
                        }
                        else if (!string.IsNullOrEmpty(errorMessage))
                        {
                            string waitMessage = FindResource("WaitBeforeCopying")?.ToString() ?? "Please wait before copying again";
                            if (errorMessage != waitMessage)
                            {
                                LogMessage($"{FindResource("ClipboardError")?.ToString() ?? "Error copying to clipboard"}: {errorMessage}", isError: true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"{FindResource("ClipboardError")?.ToString() ?? "Error copying to clipboard"}: {ex.Message}", isError: true);
                }
            };
            contextMenu.Items.Add(copySelectedMenuItem);

            System.Windows.Controls.MenuItem clearMenuItem = new System.Windows.Controls.MenuItem { Header = FindResource("ClearLog")?.ToString() ?? "Clear" };
            clearMenuItem.Click += (s, args) =>
            {
                try
                {
                    if (_logWindowTextBox != null)
                    {
                        _logWindowTextBox.Clear();
                        _logBuffer.Clear();
                        LogMessage(FindResource("LogCleared")?.ToString() ?? "Log cleared", isInfo: true);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"{FindResource("ClearLogError")?.ToString() ?? "Error clearing log"}: {ex.Message}", isError: true);
                }
            };
            contextMenu.Items.Add(clearMenuItem);

            System.Windows.Controls.MenuItem toggleAttachMenuItem = new System.Windows.Controls.MenuItem { Header = FindResource("DetachFromMainWindow")?.ToString() ?? "Detach from Main Window" };
            toggleAttachMenuItem.Click += (s, args) =>
            {
                try
                {
                    ToggleLogWindowAttachment();
                    toggleAttachMenuItem.Header = _isLogWindowAttached
                        ? FindResource("DetachFromMainWindow")?.ToString() ?? "Detach from Main Window"
                        : FindResource("AttachToMainWindow")?.ToString() ?? "Attach to Main Window";
                }
                catch (Exception ex)
                {
                    LogMessage($"{FindResource("AttachmentError")?.ToString() ?? "Error changing attachment"}: {ex.Message}", isError: true);
                }
            };
            contextMenu.Items.Add(toggleAttachMenuItem);

            _logWindowTextBox.ContextMenu = contextMenu;
            _logScrollViewer.Content = _logWindowTextBox;
            _logWindow.Content = _logScrollViewer;

            _logWindow.Closing += (s, args) =>
            {
                args.Cancel = true;
                _logWindow?.Hide(); // Use null-conditional operator
                LogMessage("Log window hidden");
                // Update tooltip on main window button
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
            };

            _logWindow.LocationChanged += LogWindow_LocationChanged;
            if (_logWindowTextBox != null)
            {
                _logWindowTextBox.Text = _logBuffer.ToString();
                _logScrollViewer?.ScrollToEnd(); // Scroll the ScrollViewer
            }

            _logWindow.Show();
            this.LocationChanged += MainWindow_LocationChanged;

            // Update tooltip on main window button
            btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
        }

        private bool _isAdjustingLogWindowPosition = false;
        private readonly System.Windows.Threading.DispatcherTimer _snapTimer = new System.Windows.Threading.DispatcherTimer();
        private bool _shouldSnap = false;

#if NET48
        private void LogWindow_LocationChanged(object sender, EventArgs e)
#else
        private void LogWindow_LocationChanged(object? sender, EventArgs e)
#endif
        {
            if (_logWindow == null || _isAdjustingLogWindowPosition) return;

            double distanceX = Math.Abs((_logWindow.Left) - (this.Left + this.Width));
            double distanceY = Math.Abs(_logWindow.Top - this.Top);

            if (_isLogWindowAttached)
            {
                if (distanceX > 20 || distanceY > 20)
                {
                    _isLogWindowAttached = false;
                    LogMessage("Log window detached", consoleOnly: true);
                    UpdateAttachmentMenuItem(false);
                }
            }
            else
            {
                bool isNearRightEdge = distanceX < 20;
                bool isVerticallyAligned = distanceY < 20;

                if (isNearRightEdge && isVerticallyAligned && !_shouldSnap)
                {
                    _shouldSnap = true;
                    _snapTimer.Stop();
                    _snapTimer.Interval = TimeSpan.FromMilliseconds(200);
                    _snapTimer.Tick += (s, args) =>
                    {
                        _snapTimer.Stop();
                        if (_shouldSnap)
                        {
                            SnapLogWindow();
                            _shouldSnap = false;
                            if (_logWindow != null)
                            {
                                _logWindow.ReleaseMouseCapture();
                            }
                        }
                    };
                    _snapTimer.Start();
                }
                else if (!isNearRightEdge || !isVerticallyAligned)
                {
                    _shouldSnap = false;
                    _snapTimer.Stop();
                }
            }
        }

        private void SnapLogWindow()
        {
            if (_logWindow == null) return;

            _isAdjustingLogWindowPosition = true;
            try
            {
                _isLogWindowAttached = true;
                _logWindow.Left = this.Left + this.Width;
                _logWindow.Top = this.Top;
                _lastMainWindowPosition = new System.Windows.Point(this.Left, this.Top);
                _logWindow.LocationChanged -= LogWindow_LocationChanged;
                _logWindow.LocationChanged += LogWindow_LocationChanged;
                LogMessage("Log window attached", consoleOnly: true);
                UpdateAttachmentMenuItem(true);
                _logWindow.UpdateLayout();
            }
            finally
            {
                _isAdjustingLogWindowPosition = false;
            }
        }

        private void UpdateAttachmentMenuItem(bool isAttached)
        {
            if (_logWindowTextBox?.ContextMenu != null)
            {
                foreach (var item in _logWindowTextBox.ContextMenu.Items)
                {
                    if (item is System.Windows.Controls.MenuItem menuItem &&
                        (menuItem.Header?.ToString()?.Contains("Detach") == true || menuItem.Header?.ToString()?.Contains("Attach") == true))
                    {
                        menuItem.Header = isAttached
                            ? FindResource("DetachFromMainWindow")?.ToString() ?? "Detach from Main Window"
                            : FindResource("AttachToMainWindow")?.ToString() ?? "Attach to Main Window";
                        break;
                    }
                }
            }
        }

        private DateTime _lastClipboardOperation = DateTime.MinValue;
        private const int CLIPBOARD_COOLDOWN_MS = 200;

        private bool SafeCopyToClipboard(string text, out string errorMessage)
        {
            errorMessage = string.Empty;
            TimeSpan timeSinceLastCopy = DateTime.Now - _lastClipboardOperation;
            if (timeSinceLastCopy.TotalMilliseconds < CLIPBOARD_COOLDOWN_MS)
            {
                errorMessage = FindResource("WaitBeforeCopying")?.ToString() ?? "Please wait before copying again";
                return false;
            }

            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            _lastClipboardOperation = DateTime.Now;

            try
            {
                // Create a new thread for clipboard operations to avoid UI thread issues
                var clipboardThread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        // Ensure we're on a thread with a message pump
                        System.Windows.Forms.Application.OleRequired();

                        // Use DataObject instead of direct clipboard access
                        System.Windows.Forms.DataObject dataObj = new System.Windows.Forms.DataObject();
                        dataObj.SetData(System.Windows.Forms.DataFormats.Text, text);

                        // Try multiple times with increasing delays
                        bool success = false;
                        int attempts = 0;
                        int maxAttempts = 5;

                        while (!success && attempts < maxAttempts)
                        {
                            try
                            {
                                System.Windows.Forms.Clipboard.SetDataObject(dataObj, true, 10, 50);
                                success = true;
                            }
                            catch
                            {
                                attempts++;
                                // Exponential backoff: 100ms, 200ms, 400ms, 800ms, 1600ms
                                int delay = 100 * (int)Math.Pow(2, attempts - 1);
                                System.Threading.Thread.Sleep(delay);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Exceptions in thread will be ignored
                    }
                });

                // Set thread as STA for clipboard operations
                clipboardThread.SetApartmentState(System.Threading.ApartmentState.STA);
                clipboardThread.Start();
                clipboardThread.Join(1000); // Wait up to 1 second for the thread to complete

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

#if NET48
        private void MainWindow_LocationChanged(object sender, EventArgs e)
#else
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
#endif
        {
            if (_logWindow == null || !_isLogWindowAttached) return;

            double deltaX = this.Left - _lastMainWindowPosition.X;
            double deltaY = this.Top - _lastMainWindowPosition.Y;

            _isAdjustingLogWindowPosition = true;
            try
            {
                _logWindow.Left += deltaX;
                _logWindow.Top += deltaY;
            }
            finally
            {
                _isAdjustingLogWindowPosition = false;
            }

            _lastMainWindowPosition = new System.Windows.Point(this.Left, this.Top);
        }

        private void ToggleLogWindowAttachment()
        {
            if (_logWindow == null) return;

            _isLogWindowAttached = !_isLogWindowAttached;

            if (_isLogWindowAttached)
            {
                SnapLogWindow();
            }
            else
            {
                LogMessage("Log window detached", consoleOnly: true);
                UpdateAttachmentMenuItem(false);
            }
        }

        private void BtnShowLog_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnShowLog_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            if (_logWindow == null || !_logWindow.IsVisible)
            {
                if (_logWindow == null)
                {
                    CreateLogWindow();
                }
                else
                {
                    if (_logWindowTextBox != null)
                    {
                        _logWindowTextBox.Text = _logBuffer.ToString();
                        _logWindowTextBox.ScrollToEnd();
                    }

                    if (_isLogWindowAttached)
                    {
                        SnapLogWindow();
                    }

                    _logWindow.Show();
                }

                LogMessage("Log window opened");
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
            }
            else
            {
                _logWindow.Hide();
                LogMessage("Log window hidden");
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
            }
        }

        public void ClearMainLog()
        {
            _logBuffer.Clear();
            if (_logWindowTextBox != null)
            {
                _logWindowTextBox.Clear();
            }
        }

        private void BtnBomSplitUnit_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnBomSplitUnit_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            string currentUnit = btnBomSplitUnit.Content?.ToString()?.ToLower() ?? "mm";
            string newUnit = currentUnit == "mm" ? "inch" : "mm";
            btnBomSplitUnit.Content = newUnit;
            LogMessage($"BomSplit unit changed to: {newUnit}");
        }

        private void BtnCadPinsUnit_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnCadPinsUnit_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");

            string currentUnit = btnCadPinsUnit.Content?.ToString()?.ToLower() ?? "mm";
            string newUnit = currentUnit == "mm" ? "inch" : "mm";
            btnCadPinsUnit.Content = newUnit;
            LogMessage($"CadPins unit changed to: {newUnit}");
        }

        private void BtnLanguageSwitch_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnLanguageSwitch_Click: Playing button click sound", consoleOnly: true);
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string soundResourceName = string.Empty;
                foreach (var resource in assembly.GetManifestResourceNames())
                {
                    if (resource.Contains("ui-minimal-click.wav"))
                    {
                        soundResourceName = resource;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(soundResourceName))
                {
                    using (var stream = assembly.GetManifestResourceStream(soundResourceName))
                    {
                        if (stream != null)
                        {
                            using (var player = new System.Media.SoundPlayer(stream))
                            {
                                player.Play();
                                LogMessage("Played sound directly from resource", consoleOnly: true);
                            }
                        }
                        else
                        {
                            LogMessage("Stream is null", consoleOnly: true);
                        }
                    }
                }
                else
                {
                    LogMessage("Sound resource not found", consoleOnly: true);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error playing sound directly: {ex.Message}", consoleOnly: true);
            }

            SoundPlayer.PlaySound("ButtonClick");
            string nextLanguage = Common.UI.Language.LanguageManager.Instance.GetNextLanguage(_config.Language);
            Common.UI.Language.LanguageManager.Instance.SwitchLanguage(nextLanguage);
            _config.Language = nextLanguage;
            _config.Save();

            if (_logWindow != null)
            {
                _logWindow.Title = FindResource("LogWindowTitle")?.ToString() ?? "Log Window";
            }

            if (_logWindow != null && _logWindow.IsVisible)
            {
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
            }
            else
            {
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
            }

            try
            {
                var assembly = Assembly.GetEntryAssembly();
                string resourceName = nextLanguage == "English"
                    ? "CSVGenerator.g.resources.app.images.united-states.png"
                    : "CSVGenerator.g.resources.app.images.romania.png";

                using (var stream = assembly?.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        imgLanguageFlag.Source = bitmap;
                        Logger.Instance.LogInfo($"Loaded flag image from embedded resource: {resourceName}", consoleOnly: true);
                    }
                    else
                    {
                        Logger.Instance.LogWarning($"Could not find embedded resource: {resourceName}", consoleOnly: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"Could not load flag image: {ex.Message}", consoleOnly: true);
            }

            string langChangedMsg = FindResource("LanguageChanged")?.ToString() ?? $"Language changed to {nextLanguage}";
            LogMessage(string.Format(langChangedMsg, nextLanguage), isInfo: true);
        }

        private void TextBox_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = System.Windows.DragDropEffects.Copy;
        }

        private void BtnTestLogger_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("BtnTestLogger_Click: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");
            LogMessage("This is an info message", isInfo: true);
            LogMessage("This is a success message", isSuccess: true);
            LogMessage("This is a warning message", isWarning: true);
            LogMessage("This is an error message", isError: true);
            LogMessage("This is a console-only message", consoleOnly: true);
        }

        private async void CheckForUpdates_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundPlayer.PlaySound("ButtonClick");
            await CheckForUpdatesAsync(userInitiated: true);
        }

        private async Task CheckForUpdatesAsync(bool userInitiated = false)
        {
            try
            {
                if (_autoUpdater == null)
                {
                    LogMessage("Auto-updater is not initialized", isWarning: true, consoleOnly: true);
                    return;
                }

                // Store the update info for reuse
                if (_latestUpdateInfo == null || userInitiated)
                {
                    var newUpdateInfo = await _autoUpdater.CheckForUpdateAsync();

                    // Check if we got a valid update info
                    if (newUpdateInfo != null)
                    {
                        _latestUpdateInfo = newUpdateInfo;
                        LogMessage($"Retrieved update info from GitHub: {_latestUpdateInfo.Version}", consoleOnly: true);
                    }
                    else
                    {
                        // If we got null from the updater but there's a message in the logs about a version
                        // This means the HTML fallback worked but returned null instead of an UpdateInfo object
                        LogMessage("GitHub API returned null update info", consoleOnly: true);
                    }
                }

                var updateInfo = _latestUpdateInfo;

                // If an update is available
                if (updateInfo != null && updateInfo.Version > _currentVersion)
                {
                    LogMessage(string.Format(FindResource("UpdateAvailable")?.ToString() ?? "Update available: v{0}", updateInfo.Version), isInfo: true);

                    if (userInitiated || System.Windows.Application.Current.Dispatcher.CheckAccess())
                    {
                        // Make sure we have release notes
                        string releaseNotes = FormatReleaseNotes(updateInfo);

                        // Create a more stylish dialog with formatted release notes
                        var updateDialog = new System.Windows.Window
                        {
                            Title = FindResource("MenuCheckForUpdates")?.ToString() ?? "Check for Updates",
                            Width = 800,  // Even larger width
                            Height = 600, // Even larger height
                            WindowStartupLocation = WindowStartupLocation.CenterScreen, // Center on screen instead of owner
                            Owner = this,
                            ResizeMode = ResizeMode.CanResize, // Allow resizing
                            WindowStyle = WindowStyle.SingleBorderWindow, // Normal window style
                            MinWidth = 600, // Larger minimum size
                            MinHeight = 500
                        };

                        // Create the content
                        var grid = new Grid();
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // Header
                        var header = new TextBlock
                        {
                            Text = string.Format(FindResource("UpdateAvailable")?.ToString() ?? "Update available: v{0}", updateInfo.Version) +
                                   $"\nCurrent version: v{_currentVersion}",
                            Margin = new Thickness(10),
                            TextWrapping = TextWrapping.Wrap,
                            FontWeight = FontWeights.Bold
                        };
                        Grid.SetRow(header, 0);
                        grid.Children.Add(header);

                        // Release notes in a scrollable area
                        var scrollViewer = new ScrollViewer
                        {
                            Margin = new Thickness(10, 0, 10, 10),
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                        };
                        Grid.SetRow(scrollViewer, 1);

                        // Use a FlowDocument for rich text formatting
                        var richTextBox = new System.Windows.Controls.RichTextBox
                        {
                            IsReadOnly = true,
                            BorderThickness = new Thickness(0),
                            Background = System.Windows.Media.Brushes.Transparent
                        };

                        // Convert markdown with colors to FlowDocument
                        var flowDocument = new FlowDocument();
                        var paragraph = new Paragraph();

                        // Process the colored text
                        RenderColoredText(releaseNotes, paragraph);

                        flowDocument.Blocks.Add(paragraph);
                        richTextBox.Document = flowDocument;

                        scrollViewer.Content = richTextBox;
                        grid.Children.Add(scrollViewer);

                        // Buttons
                        var buttonPanel = new System.Windows.Controls.StackPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            Margin = new Thickness(10)
                        };
                        Grid.SetRow(buttonPanel, 2);

                        var yesButton = new System.Windows.Controls.Button
                        {
                            Content = "Yes",
                            Padding = new Thickness(20, 5, 20, 5),
                            Margin = new Thickness(5),
                            IsDefault = true
                        };

                        var noButton = new System.Windows.Controls.Button
                        {
                            Content = "No",
                            Padding = new Thickness(20, 5, 20, 5),
                            Margin = new Thickness(5),
                            IsCancel = true
                        };

                        var viewOnGitHubButton = new System.Windows.Controls.Button
                        {
                            Content = "View on GitHub",
                            Padding = new Thickness(20, 5, 20, 5),
                            Margin = new Thickness(5)
                        };

                        buttonPanel.Children.Add(viewOnGitHubButton);
                        buttonPanel.Children.Add(yesButton);
                        buttonPanel.Children.Add(noButton);
                        grid.Children.Add(buttonPanel);

                        updateDialog.Content = grid;

                        // Set up button click handlers
                        bool result = false;
                        yesButton.Click += (s, args) => { result = true; updateDialog.Close(); };
                        noButton.Click += (s, args) => { result = false; updateDialog.Close(); };
                        viewOnGitHubButton.Click += (s, args) =>
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = updateInfo.ReleaseUrl,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Error opening GitHub: {ex.Message}", isError: true);
                            }
                        };

                        // Show dialog
                        updateDialog.ShowDialog();

                        // Process result
                        if (result)
                        {
                            LogMessage(FindResource("DownloadingUpdate")?.ToString() ?? "Downloading update...", isInfo: true);
                            try
                            {
                                bool success = await _autoUpdater.DownloadAndInstallUpdateAsync(updateInfo);
                                if (!success)
                                {
                                    LogMessage(FindResource("UpdateFailed")?.ToString() ?? "Update failed.", isWarning: true);
                                    _autoUpdater.OpenUpdateUrl(updateInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage(string.Format(FindResource("ErrorInstallingUpdate")?.ToString() ?? "Error installing update: {0}", ex.Message), isError: true);
                                _autoUpdater.OpenUpdateUrl(updateInfo);
                            }
                        }
                    }
                }
                // No update available or user-initiated check
                else if (userInitiated && System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    // Always show dialog for user-initiated checks
                    string latestVersionText = updateInfo != null ? updateInfo.Version.ToString() : "N/A"; // N/A if GitHub version is unavailable

                    // Make sure we have release notes
                    string releaseNotes;
                    if (updateInfo != null)
                    {
                        releaseNotes = FormatReleaseNotes(updateInfo);
                    }
                    else
                    {
                        releaseNotes = "# <color=#FF5722>Unable to retrieve version information</color>\n\n" +
                                      "<color=#5D5D5D>Could not connect to GitHub to check for updates.</color>\n\n" +
                                      "<color=#107C10>Possible reasons:</color>\n" +
                                      "<color=#5D5D5D>- No internet connection</color>\n" +
                                      "<color=#5D5D5D>- GitHub repository is unavailable</color>\n" +
                                      "<color=#5D5D5D>- Network restrictions</color>\n\n" +
                                      "<color=#0078D7>You can try again later or visit the GitHub page manually.</color>";
                    }

                    LogMessage($"Showing update dialog with latest version: {latestVersionText}", isInfo: true, consoleOnly: true);

                    // Create a more stylish dialog with formatted release notes
                    var updateDialog = new System.Windows.Window
                    {
                        Title = FindResource("MenuCheckForUpdates")?.ToString() ?? "Check for Updates",
                        Width = 800,  // Even larger width
                        Height = 600, // Even larger height
                        WindowStartupLocation = WindowStartupLocation.CenterScreen, // Center on screen instead of owner
                        Owner = this,
                        ResizeMode = ResizeMode.CanResize, // Allow resizing
                        WindowStyle = WindowStyle.SingleBorderWindow, // Normal window style
                        MinWidth = 600, // Larger minimum size
                        MinHeight = 500
                    };

                    // Create the content
                    var grid = new Grid();
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Header
                    var header = new TextBlock
                    {
                        Text = updateInfo != null
                            ? (FindResource("NoUpdatesAvailable")?.ToString() ?? "No updates available.") + $" (v{_currentVersion})\n" +
                              $"Latest version on GitHub: v{latestVersionText}"
                            : $"Current version: v{_currentVersion}\n" +
                              "Unable to retrieve latest version from GitHub",
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeights.Bold
                    };
                    Grid.SetRow(header, 0);
                    grid.Children.Add(header);

                    // Release notes in a scrollable area
                    var scrollViewer = new ScrollViewer
                    {
                        Margin = new Thickness(10, 0, 10, 10),
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                    Grid.SetRow(scrollViewer, 1);

                    // Use a FlowDocument for rich text formatting
                    var richTextBox = new System.Windows.Controls.RichTextBox
                    {
                        IsReadOnly = true,
                        BorderThickness = new Thickness(0),
                        Background = System.Windows.Media.Brushes.Transparent
                    };

                    // Convert markdown with colors to FlowDocument
                    var flowDocument = new FlowDocument();
                    var paragraph = new Paragraph();

                    // Process the colored text
                    RenderColoredText(releaseNotes, paragraph);

                    flowDocument.Blocks.Add(paragraph);
                    richTextBox.Document = flowDocument;

                    scrollViewer.Content = richTextBox;
                    grid.Children.Add(scrollViewer);

                    // Buttons
                    var buttonPanel = new System.Windows.Controls.StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Margin = new Thickness(10)
                    };
                    Grid.SetRow(buttonPanel, 2);

                    var yesButton = new System.Windows.Controls.Button
                    {
                        Content = updateInfo != null ? "Visit GitHub" : "Try GitHub Anyway",
                        Padding = new Thickness(20, 5, 20, 5),
                        Margin = new Thickness(5),
                        IsDefault = true
                    };

                    var noButton = new System.Windows.Controls.Button
                    {
                        Content = "Close",
                        Padding = new Thickness(20, 5, 20, 5),
                        Margin = new Thickness(5),
                        IsCancel = true
                    };

                    buttonPanel.Children.Add(yesButton);
                    buttonPanel.Children.Add(noButton);
                    grid.Children.Add(buttonPanel);

                    updateDialog.Content = grid;

                    // Set up button click handlers
                    bool result = false;
                    yesButton.Click += (s, args) => { result = true; updateDialog.Close(); };
                    noButton.Click += (s, args) => { result = false; updateDialog.Close(); };

                    // Show dialog
                    updateDialog.ShowDialog();

                    // Process result
                    if (result)
                    {
                        LogMessage(FindResource("ForcingUpdate")?.ToString() ?? "Forcing update...", isInfo: true);

                        // If we have updateInfo, use it; otherwise create a new one
                        if (updateInfo != null)
                        {
                            _autoUpdater.OpenUpdateUrl(updateInfo);
                        }
                        else
                        {
                            // If latestVersionText is "N/A", use a default URL without version
                            if (latestVersionText == "N/A")
                            {
                                // Just open the GitHub releases page directly
                                try
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "https://github.com/DarkPhilosophy/CSVGenerator/releases/latest",
                                        UseShellExecute = true
                                    });
                                    LogMessage("Opened GitHub releases page directly", isInfo: true);
                                }
                                catch (Exception ex)
                                {
                                    LogMessage($"Error opening GitHub releases page: {ex.Message}", isError: true);
                                }
                            }
                            else
                            {
                            var forcedUpdateInfo = new Common.Update.UpdateInfo(
                                new Version(latestVersionText),
                                "https://github.com/DarkPhilosophy/CSVGenerator/releases/latest",
                                "https://github.com/DarkPhilosophy/CSVGenerator/releases/latest",
                                "Forced update by user",
                                "",            // sha256, default to empty string
                                false,         // isMandatory
                                DateTime.Now,  // publishedDate
                                true           // updateNeeded
                            );
                            _autoUpdater.OpenUpdateUrl(forcedUpdateInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(FindResource("ErrorCheckingForUpdates")?.ToString() ?? "Error checking for updates: {0}", ex.Message), isError: true);
            }
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            LogMessage("ComboBox_DropDownOpened: Playing button click sound", consoleOnly: true);
            SoundPlayer.PlaySound("ButtonClick");
        }

        private void ProgramComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox)
            {
                LogMessage($"Program ComboBox loaded with {comboBox.Items.Count} items", consoleOnly: true);

                // Force template to update based on current item count
                comboBox.UpdateLayout();
            }
        }

        private void ProgramComboBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // This handler is still needed for the XAML binding, but we don't need to do anything here
            // The DataTrigger in the style will handle switching templates based on item count
        }

        private void BomSplitPath_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    txtBomSplitPath.Text = files[0];
                    _config.LastBomSplitPath = files[0];
                    _config.Save();
                    string unit = FileParser.Instance.DetectUnit(files[0]);
                    btnBomSplitUnit.Content = unit;
                    LogMessage($"BomSplit file dropped: {Path.GetFileName(files[0])}, unit: {unit}");
                }
            }
        }

        private void CadPinsPath_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    txtCadPinsPath.Text = files[0];
                    _config.LastCadPinsPath = files[0];
                    _config.Save();
                    string unit = FileParser.Instance.DetectUnit(files[0]);
                    btnCadPinsUnit.Content = unit;
                    LogMessage($"CadPins file dropped: {Path.GetFileName(files[0])}, unit: {unit}");
                }
            }
        }

        private void CmbProgram_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isProgramBeingDeleted)
            {
                return;
            }

            if (e.OriginalSource is System.Windows.Controls.ComboBox && Mouse.DirectlyOver is System.Windows.Controls.Button button &&
                button.Name == "ProgramDeleteButton")
            {
                return;
            }

            string text = cmbProgram.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (_programBeingDeleted != null && string.Equals(text, _programBeingDeleted, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                bool exists = false;
                foreach (string item in cmbProgram.Items)
                {
                    if (string.Equals(item, text, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    cmbProgram.Items.Add(text);
                    cmbProgram.SelectedItem = text;
                    _config.ProgramHistory.Add(text);
                    _config.Save();
                    LogMessage($"Added new program: {text}");

                    // Force template to update based on current item count
                    cmbProgram.UpdateLayout();
                }
            }
        }

        private void ProgramDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is string programName)
                {
                    _isProgramBeingDeleted = true;
                    _programBeingDeleted = programName;
                    string currentText = cmbProgram.Text;
                    cmbProgram.Items.Remove(programName);

                    if (_config.ProgramHistory.Contains(programName))
                    {
                        _config.ProgramHistory.Remove(programName);
                        _config.Save();
                    }

                    cmbProgram.SelectedIndex = -1;

                    if (!string.Equals(currentText, programName, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(currentText))
                    {
                        cmbProgram.Text = currentText;
                    }
                    else if (cmbProgram.Items.Count > 0)
                    {
                        cmbProgram.SelectedIndex = 0;
                    }
                    else
                    {
                        cmbProgram.Text = string.Empty;
                    }

                    // Force template to update based on current item count
                    cmbProgram.UpdateLayout();

                    LogMessage($"Removed program: {programName}");
                    e.Handled = true;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isProgramBeingDeleted = false;
                        _programBeingDeleted = null;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error removing program: {ex.Message}");
                e.Handled = true;
                _isProgramBeingDeleted = false;
                _programBeingDeleted = null;
            }
        }

        private void Copyright_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundPlayer.PlaySound("ButtonClick");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/DarkPhilosophy/CSVGenerator",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error opening GitHub repository: {ex.Message}", isError: true);
            }
        }



        /// <summary>
        /// Applies color tags to specific keywords in markdown text.
        /// </summary>
        /// <param name="markdown">Input markdown text.</param>
        /// <returns>Markdown with color tags applied.</returns>
        private string AddColorsToMarkdown(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return markdown;

            var replacements = new Dictionary<string, string>
            {
                { "CSVGenerator", "<color=#0078D7>CSVGenerator</color>" },
                { "Initial release", "<color=#107C10>Initial release</color>" },
                { "GitHub", "<color=#0066CC>GitHub</color>" },
                { "Changes", "<color=#107C10>Changes</color>" }
            };

            foreach (var kvp in replacements)
            {
                markdown = System.Text.RegularExpressions.Regex.Replace(
                    markdown,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(kvp.Key)}\b",
                    kvp.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            return markdown;
        }

        /// <summary>
        /// Formats release notes with colored markdown for display.
        /// </summary>
        /// <param name="updateInfo">Update information containing release notes.</param>
        /// <returns>Formatted release notes string.</returns>
        private string FormatReleaseNotes(Common.Update.UpdateInfo updateInfo)
        {
            if (updateInfo == null)
                return "No release information available.";

            // Get the raw release notes from GitHub
            string releaseNotes = updateInfo.ReleaseNotes;

            // Log the raw release notes for debugging
            LogMessage($"ORIGINAL RAW RELEASE NOTES: {releaseNotes}", consoleOnly: true);

            // Extract the content after "## Changes" if present
            string changesContent = "";
            if (releaseNotes.Contains("## Changes"))
            {
                int changesIndex = releaseNotes.IndexOf("## Changes");
                if (changesIndex >= 0)
                {
                    // Skip the "## Changes" line
                    int contentStart = releaseNotes.IndexOf('\n', changesIndex);
                    if (contentStart >= 0)
                    {
                        // Extract only the content after "## Changes"
                        changesContent = releaseNotes.Substring(contentStart).Trim();
                        LogMessage($"CHANGES CONTENT: {changesContent}", consoleOnly: true);
                    }
                }
            }
            else
            {
                // If no "## Changes" section, use the entire content
                changesContent = releaseNotes;
            }

            if (string.IsNullOrWhiteSpace(changesContent) ||
                changesContent.Contains("See release notes on GitHub") ||
                changesContent.Contains("Could not retrieve release notes") ||
                changesContent.Contains("Forced update by user"))
            {
                try
                {
                    LogMessage($"Fetching release notes from: {updateInfo.ReleaseUrl}", consoleOnly: true);
                    return $"# <color=#0078D7>CSVGenerator v{updateInfo.Version}</color>\n\n" +
                        $"<color=#5D5D5D>Build Date: {updateInfo.PublishedDate:yyyy-MM-dd HH:mm:ss}</color>\n\n" +
                        $"<color=#5D5D5D>SHA256: {updateInfo.Sha256}</color>\n\n" +
                        "## <color=#107C10>Release Notes</color>\n" +
                        "Release notes available on GitHub.\n\n" +
                        $"<color=#0066CC>[View on GitHub]({updateInfo.ReleaseUrl})</color>";
                }
                catch (Exception ex)
                {
                    LogMessage($"Error fetching release notes: {ex.Message}", consoleOnly: true);
                    return $"# <color=#0078D7>CSVGenerator v{updateInfo.Version}</color>\n\n" +
                        $"<color=#5D5D5D>Build Date: {updateInfo.PublishedDate:yyyy-MM-dd HH:mm:ss}</color>\n\n" +
                        $"<color=#5D5D5D>SHA256: {updateInfo.Sha256}</color>\n\n" +
                        "## <color=#107C10>Release Notes</color>\n" +
                        "<color=#FF5722>Release notes unavailable.</color>\n\n" +
                        $"<color=#0066CC>[Check GitHub]({updateInfo.ReleaseUrl})</color>";
                }
            }

            // Normalize line endings
            changesContent = changesContent.Replace("\r\n", "\n").Replace("\r", "\n");

            // Format the bullet points with proper line breaks
            var formattedBulletPoints = FormatBulletPoints(changesContent);
            LogMessage($"FORMATTED BULLET POINTS: {formattedBulletPoints}", consoleOnly: true);

            // Create the final formatted output
            return $"# <color=#0078D7>CSVGenerator v{updateInfo.Version}</color>\n\n" +
                $"<color=#5D5D5D>Build Date: {updateInfo.PublishedDate:yyyy-MM-dd HH:mm:ss}</color>\n\n" +
                $"<color=#5D5D5D>SHA256: {updateInfo.Sha256}</color>\n\n" +
                "## <color=#107C10>Changes</color>\n\n" +
                formattedBulletPoints;
        }

        /// <summary>
        /// Formats bullet points with proper line breaks.
        /// </summary>
        /// <param name="content">Raw content with bullet points.</param>
        /// <returns>Formatted bullet points with proper line breaks.</returns>
        private string FormatBulletPoints(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            LogMessage($"FormatBulletPoints input: {content}", consoleOnly: true);

            // Split the content into lines
            var lines = content.Split('\n').Select(l => l.Trim()).ToList();
            var result = new StringBuilder();

            // Process each line
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Add empty lines to maintain spacing
                    result.AppendLine();
                    continue;
                }

                // If the line starts with a bullet point, add it directly
                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    result.AppendLine(line);
                }
                // Otherwise, check if it's a continuation of a bullet point
                else if (line.Contains(" - "))
                {
                    // Split by " - " to separate potential bullet points
                    var parts = line.Split(new[] { " - " }, StringSplitOptions.None);

                    // Add the first part
                    result.AppendLine($"- {parts[0]}");

                    // Add the remaining parts as separate bullet points
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(parts[i]))
                        {
                            result.AppendLine($"- {parts[i]}");
                        }
                    }
                }
                // If it's just regular text, make it a bullet point
                else
                {
                    result.AppendLine($"- {line}");
                }
            }

            string formatted = result.ToString().TrimEnd();
            LogMessage($"FormatBulletPoints output: {formatted}", consoleOnly: true);

            // Apply color formatting
            return AddColorsToMarkdown(formatted);
        }

        /// <summary>
        /// Renders colored markdown text to a WPF Paragraph for RichTextBox.
        /// </summary>
        /// <param name="markdown">Markdown text with <color> tags.</param>
        /// <param name="paragraph">Optional Paragraph to append to; if null, a new one is created.</param>
        /// <returns>Paragraph with formatted text.</returns>
        private Paragraph RenderColoredText(string markdown, Paragraph paragraph = null)
        {
            if (paragraph == null)
                paragraph = new Paragraph();

            if (string.IsNullOrWhiteSpace(markdown))
            {
                paragraph.Inlines.Add(new Run("No content"));
                return paragraph;
            }

            var lines = markdown.Split('\n').Select(l => l.TrimEnd()).ToList();
            var colorRegex = new System.Text.RegularExpressions.Regex(@"<color=#([0-9A-Fa-f]{6})>(.*?)</color>", System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    paragraph.Inlines.Add(new LineBreak());
                    continue;
                }

                double fontSize = 16; // Default
                bool isBold = false;
                string text = line;

                // Handle markdown headers, including those with pre-applied color tags
                if (line.StartsWith("# ") || line.StartsWith("# <color="))
                {
                    fontSize = 24;
                    isBold = true;
                    if (line.StartsWith("# "))
                        text = line.Substring(2);
                    else
                        text = line.Substring(2); // Strip "# " even with color tag
                }
                else if (line.StartsWith("## ") || line.StartsWith("## <color="))
                {
                    fontSize = 20;
                    isBold = true;
                    if (line.StartsWith("## "))
                        text = line.Substring(3);
                    else
                        text = line.Substring(3); // Strip "## " even with color tag
                }

                int lastIndex = 0;
                foreach (System.Text.RegularExpressions.Match match in colorRegex.Matches(text))
                {
                    if (match.Index > lastIndex)
                    {
                        string beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                        var run = new Run(beforeText)
                        {
                            FontSize = fontSize,
                            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                            FontWeight = isBold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
                        };
                        if (line.StartsWith("- ") || line.StartsWith("* "))
                        {
                            run.Text = $"  {beforeText}";
                        }
                        paragraph.Inlines.Add(run);
                    }

                    string colorHex = match.Groups[1].Value;
                    string coloredText = match.Groups[2].Value;
                    var brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString($"#{colorHex}"));
                    var coloredRun = new Run(coloredText)
                    {
                        Foreground = brush,
                        FontSize = fontSize,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        FontWeight = isBold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
                    };
                    if (line.StartsWith("- ") || line.StartsWith("* ") && match.Index == 0)
                    {
                        coloredRun.Text = $"  {coloredText}";
                    }
                    paragraph.Inlines.Add(coloredRun);

                    lastIndex = match.Index + match.Length;
                }

                if (lastIndex < text.Length)
                {
                    string remainingText = text.Substring(lastIndex);
                    var run = new Run(remainingText)
                    {
                        FontSize = fontSize,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        FontWeight = isBold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
                    };
                    if (line.StartsWith("- ") || line.StartsWith("* ") && lastIndex == 0)
                    {
                        run.Text = $"  {remainingText}";
                    }
                    paragraph.Inlines.Add(run);
                }

                paragraph.Inlines.Add(new LineBreak());
            }

            return paragraph;
        }

        private async Task LoadAndDisplayAds(TextBlock txtAdBanner, Grid adContainer, Border adBannerContainer)
        {
            try
            {
                // Hide ad containers by default until ads are successfully loaded
                txtAdBanner.Text = string.Empty;
                adBannerContainer.Visibility = Visibility.Collapsed;
                adContainer.Visibility = Visibility.Collapsed;

                // Initialize the UniversalAdLoader first
                await Task.Run(() => {
                    // Initialize the UniversalAdLoader with the log callback
                    UniversalAdLoader.Instance.Initialize((message, isError, isWarning, isSuccess, isInfo, consoleOnly, data) =>
                        LogMessage(message, isError, isWarning, isSuccess, isInfo, consoleOnly));
                });

                // Create an adapter for the UniversalAdLoader to implement IAdLoader
                var adLoaderAdapter = new UniversalAdLoaderAdapter(UniversalAdLoader.Instance);

                // Initialize the AdManager with our UI elements and logging
                Common.UI.Ads.AdManager.Instance.Initialize(
                    txtAdBanner,
                    adContainer,
                    new Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>>((message, isError, isWarning, isInfo, isDebug, consoleOnly, data) =>
                    {
                        LogMessage(message, isError, isWarning, false, isInfo, consoleOnly);
                    }),
                    adLoaderAdapter
                );

                LogMessage("Ad system initialized asynchronously", consoleOnly: true);
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing ad system: {ex.Message}", isError: true);

                // Keep ad containers hidden on error
                txtAdBanner.Text = string.Empty;
                adBannerContainer.Visibility = Visibility.Collapsed;
                adContainer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Adapter class to make UniversalAdLoader compatible with IAdLoader interface
        /// </summary>
        private class UniversalAdLoaderAdapter : Common.UI.Ads.IAdLoader
        {
            private readonly UniversalAdLoader _adLoader;
#if NET48
            private Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>> _logCallback;
#else
            private Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>>? _logCallback;
#endif

            public UniversalAdLoaderAdapter(UniversalAdLoader adLoader)
            {
                _adLoader = adLoader;
#if NET48
                _logCallback = (msg, err, warn, succ, info, console, data) => { /* Default no-op */ };
#endif
            }

#if NET48
            public void Initialize(Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>> logCallback)
#else
            public void Initialize(Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>>? logCallback)
#endif
            {
                _logCallback = logCallback ?? ((msg, err, warn, succ, info, console, data) => { /* No-op if null */ });
            }

            public async Task<Common.UI.Ads.ImageAdMetadata> LoadAdMetadataAsync()
            {
                try
                {
                    var metadata = await _adLoader.LoadAdMetadataAsync();
                    var result = new Common.UI.Ads.ImageAdMetadata();

                    // Helper function to find a key case-insensitively
                    string FindKey(Dictionary<string, object> dict, string keyToFind)
                    {
                        return dict.Keys.FirstOrDefault(k => k.ToLower() == keyToFind.ToLower());
                    }

                    // Convert text ads - use case-insensitive key comparison
                    string textsKey = FindKey(metadata, "texts");
                    if (!string.IsNullOrEmpty(textsKey))
                    {
                        result.Texts = new List<Common.UI.Ads.TextAd>();
                        Log($"Found texts key: {textsKey}");

                        if (metadata[textsKey] is List<Dictionary<string, object>> textAdsList)
                        {
                            foreach (var ad in textAdsList)
                            {
                                AddTextAd(result, ad);
                            }
                        }
                        else if (metadata[textsKey] is Newtonsoft.Json.Linq.JArray textJArray)
                        {
                            Log($"Found {textJArray.Count} text ads in JArray");
                            foreach (var item in textJArray)
                            {
                                if (item is Newtonsoft.Json.Linq.JObject jObj)
                                {
                                    var dict = jObj.ToObject<Dictionary<string, object>>();
                                    if (dict != null)
                                    {
                                        AddTextAd(result, dict);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Log("No texts key found in metadata");
                    }

                    // Convert image ads - use case-insensitive key comparison
                    string imagesKey = FindKey(metadata, "images");
                    if (!string.IsNullOrEmpty(imagesKey))
                    {
                        result.Images = new List<Common.UI.Ads.ImageAd>();
                        Log($"Found images key: {imagesKey}");

                        if (metadata[imagesKey] is List<Dictionary<string, object>> imageAdsList)
                        {
                            foreach (var ad in imageAdsList)
                            {
                                AddImageAd(result, ad);
                            }
                        }
                        else if (metadata[imagesKey] is Newtonsoft.Json.Linq.JArray imageJArray)
                        {
                            Log($"Found {imageJArray.Count} image ads in JArray");
                            foreach (var item in imageJArray)
                            {
                                if (item is Newtonsoft.Json.Linq.JObject jObj)
                                {
                                    var dict = jObj.ToObject<Dictionary<string, object>>();
                                    if (dict != null)
                                    {
                                        AddImageAd(result, dict);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Log("No images key found in metadata");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Log($"Error loading ad metadata: {ex.Message}");
                    return new Common.UI.Ads.ImageAdMetadata();
                }
            }

            private void AddTextAd(Common.UI.Ads.ImageAdMetadata metadata, Dictionary<string, object> ad)
            {
                try
                {
                    // Use case-insensitive key comparison
                    string GetStringValue(Dictionary<string, object> dict, string key)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null && dict[keyFound] is string value)
                            return value;
                        return string.Empty;
                    }

                    int GetIntValue(Dictionary<string, object> dict, string key, int defaultValue)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null)
                        {
                            var value = dict[keyFound];
                            if (value is int intValue)
                                return intValue;
                            else if (value is long longValue)
                                return (int)longValue;
                            else if (value is double doubleValue)
                                return (int)doubleValue;
                            else if (value is Newtonsoft.Json.Linq.JValue jValue)
                            {
                                // Try to convert JValue to int
                                try
                                {
                                    return jValue.ToObject<int>();
                                }
                                catch
                                {
                                    // If conversion fails, try to get the raw value and convert it
                                    var rawValue = jValue.Value;
                                    if (rawValue is int i) return i;
                                    if (rawValue is long l) return (int)l;
                                    if (rawValue is double d) return (int)d;
                                    if (rawValue is string s && int.TryParse(s, out int result)) return result;
                                }
                            }
                            else if (value is string stringValue && int.TryParse(stringValue, out int parsedValue))
                                return parsedValue;

                            // Log the actual type for debugging
                            Log($"Could not convert '{key}' value to int. Type: {value?.GetType().Name ?? "null"}, Value: {value}");
                        }
                        return defaultValue;
                    }

                    long GetLongValue(Dictionary<string, object> dict, string key, long defaultValue)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null && dict[keyFound] is long value)
                            return value;
                        return defaultValue;
                    }

                    List<string> GetLanguages(Dictionary<string, object> dict)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == "languages");
                        if (keyFound != null && dict[keyFound] is Newtonsoft.Json.Linq.JArray langArray)
                        {
                            var languages = new List<string>();
                            foreach (var lang in langArray)
                            {
                                if (lang.ToString() is string langStr)
                                    languages.Add(langStr);
                            }
                            return languages.Count > 0 ? languages : new List<string> { "all" };
                        }
                        return new List<string> { "all" };
                    }

                    var textAd = new Common.UI.Ads.TextAd
                    {
                        Id = GetIntValue(ad, "id", 0),
                        Description = GetStringValue(ad, "description"), // Look for "description" instead of "Text"
                        Url = GetStringValue(ad, "url"),
                        Duration = GetIntValue(ad, "duration", 15), // Default duration
                        Languages = GetLanguages(ad),
                        Timestamp = GetLongValue(ad, "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds())
                    };

                    Log($"Added text ad: {textAd.Description.Substring(0, Math.Min(30, textAd.Description.Length))}... (ID: {textAd.Id}, Duration: {textAd.Duration}s)");

                    // Add color formatting for better display
                    if (!string.IsNullOrEmpty(textAd.Description) && !textAd.Description.Contains("#["))
                    {
                        // Add vibrant colors to make it more interesting
                        if (textAd.Description.Contains("CSV") || textAd.Description.Contains("Generator"))
                        {
                            // Highlight product name in blue
                            textAd.Description = textAd.Description.Replace("CSVGenerator", "#[Blue]CSVGenerator#");
                            textAd.Description = textAd.Description.Replace("CSV Generator", "#[Blue]CSV Generator#");
                        }
                        else if (textAd.Description.Contains("GitHub"))
                        {
                            // Highlight GitHub in purple
                            textAd.Description = textAd.Description.Replace("GitHub", "#[Purple]GitHub#");
                        }
                        else if (textAd.Description.Contains("update") || textAd.Description.Contains("Update"))
                        {
                            // Highlight updates in green
                            textAd.Description = textAd.Description.Replace("update", "#[Green]update#");
                            textAd.Description = textAd.Description.Replace("Update", "#[Green]Update#");
                        }
                        // No default formatting or welcome message
                    }

                    metadata.Texts.Add(textAd);
                }
                catch (Exception ex)
                {
                    Log($"Error adding text ad: {ex.Message}");
                }
            }

            private void AddImageAd(Common.UI.Ads.ImageAdMetadata metadata, Dictionary<string, object> ad)
            {
                try
                {
                    // Use case-insensitive key comparison
                    string GetStringValue(Dictionary<string, object> dict, string key)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null && dict[keyFound] is string value)
                            return value;
                        return string.Empty;
                    }

                    int GetIntValue(Dictionary<string, object> dict, string key, int defaultValue)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null)
                        {
                            var value = dict[keyFound];
                            if (value is int intValue)
                                return intValue;
                            else if (value is long longValue)
                                return (int)longValue;
                            else if (value is double doubleValue)
                                return (int)doubleValue;
                            else if (value is Newtonsoft.Json.Linq.JValue jValue)
                            {
                                // Try to convert JValue to int
                                try
                                {
                                    return jValue.ToObject<int>();
                                }
                                catch
                                {
                                    // If conversion fails, try to get the raw value and convert it
                                    var rawValue = jValue.Value;
                                    if (rawValue is int i) return i;
                                    if (rawValue is long l) return (int)l;
                                    if (rawValue is double d) return (int)d;
                                    if (rawValue is string s && int.TryParse(s, out int result)) return result;
                                }
                            }
                            else if (value is string stringValue && int.TryParse(stringValue, out int parsedValue))
                                return parsedValue;

                            // Log the actual type for debugging
                            Log($"Could not convert '{key}' value to int. Type: {value?.GetType().Name ?? "null"}, Value: {value}");
                        }
                        return defaultValue;
                    }

                    long GetLongValue(Dictionary<string, object> dict, string key, long defaultValue)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == key.ToLower());
                        if (keyFound != null && dict[keyFound] is long value)
                            return value;
                        return defaultValue;
                    }

                    List<string> GetLanguages(Dictionary<string, object> dict)
                    {
                        var keyFound = dict.Keys.FirstOrDefault(k => k.ToLower() == "languages");
                        if (keyFound != null && dict[keyFound] is Newtonsoft.Json.Linq.JArray langArray)
                        {
                            var languages = new List<string>();
                            foreach (var lang in langArray)
                            {
                                if (lang.ToString() is string langStr)
                                    languages.Add(langStr);
                            }
                            return languages.Count > 0 ? languages : new List<string> { "all" };
                        }
                        return new List<string> { "all" };
                    }

                    var imageAd = new Common.UI.Ads.ImageAd
                    {
                        Id = GetIntValue(ad, "id", 0),
                        File = GetStringValue(ad, "file"), // Look for "file" instead of "Filename"
                        Description = GetStringValue(ad, "description"),
                        Url = GetStringValue(ad, "url"),
                        Duration = GetIntValue(ad, "duration", 5), // Default duration in seconds
                        Languages = GetLanguages(ad),
                        Timestamp = GetLongValue(ad, "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds())
                    };

                    Log($"Added image ad: {imageAd.File} (ID: {imageAd.Id}, Duration: {imageAd.Duration}s)");

                    metadata.Images.Add(imageAd);
                }
                catch (Exception ex)
                {
                    Log($"Error adding image ad: {ex.Message}");
                }
            }

            public Task<List<string>> LoadTextAdsFromFileAsync()
            {
                // This is a fallback method that's not really used with our implementation
                return Task.FromResult(new List<string>());
            }

#if NET48
            public async Task<byte[]> LoadImageFileAsync(string filename)
#else
            public async Task<byte[]?> LoadImageFileAsync(string filename)
#endif
            {
                try
                {
                    var imageData = await _adLoader.LoadImageFileAsync(filename);
                    if (imageData == null || imageData.Length == 0)
                    {
                        Log($"No image data found for {filename}");
                        return new byte[0];
                    }
                    return imageData;
                }
                catch (Exception ex)
                {
                    Log($"Error loading image file {filename}: {ex.Message}");
                    return new byte[0];
                }
            }

            public string TimestampToString(long timestamp)
            {
                try
                {
                    var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                    return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch
                {
                    return "Unknown date";
                }
            }

#if NET48
            public Task<string> FindImageFileAsync(string fileName)
            {
                // We don't need to implement this as our UniversalAdLoader handles file paths
                return Task.FromResult(fileName);
            }
#else
            public Task<string?> FindImageFileAsync(string fileName)
            {
                // We don't need to implement this as our UniversalAdLoader handles file paths
                return Task.FromResult<string?>(fileName);
            }
#endif

            public Common.UI.Ads.ImageAdMetadata GetCachedMetadata()
            {
                // Return empty metadata as we don't cache in this adapter
                return new Common.UI.Ads.ImageAdMetadata();
            }

            private void Log(string message)
            {
                _logCallback?.Invoke(message, false, false, false, true, true, new Dictionary<string, object>());
            }
        }

        private void LoadImagesFromEmbeddedResources()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                var btnShowLog = FindName("btnShowLog") as System.Windows.Controls.Button;
                if (btnShowLog != null)
                {
                    LoadImageToButton(btnShowLog, "CSVGenerator.g.resources.app.images.playlist.png");
                }

                var btnSelectBomSplit = FindName("btnSelectBomSplit") as System.Windows.Controls.Button;
                if (btnSelectBomSplit != null)
                {
                    var stackPanel = btnSelectBomSplit.Content as StackPanel;
                    if (stackPanel != null)
                    {
                        var image = new System.Windows.Controls.Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 5, 0) };
                        LoadImageToControl(image, "CSVGenerator.g.resources.app.images.upload-file.png");
                        stackPanel.Children.Insert(0, image);
                    }
                }

                var btnSelectCadPins = FindName("btnSelectCadPins") as System.Windows.Controls.Button;
                if (btnSelectCadPins != null)
                {
                    var stackPanel = btnSelectCadPins.Content as StackPanel;
                    if (stackPanel != null)
                    {
                        var image = new System.Windows.Controls.Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 5, 0) };
                        LoadImageToControl(image, "CSVGenerator.g.resources.app.images.upload-file.png");
                        stackPanel.Children.Insert(0, image);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"Error loading images from embedded resources: {ex.Message}", consoleOnly: true);
            }
        }

        private void LoadImageToButton(System.Windows.Controls.Button button, string resourceName)
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                using (var stream = assembly?.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        var image = new System.Windows.Controls.Image { Source = bitmap, Width = 24, Height = 24 };
                        button.Content = image;
                        Logger.Instance.LogInfo($"Loaded image from embedded resource: {resourceName}", consoleOnly: true);
                    }
                    else
                    {
                        Logger.Instance.LogWarning($"Could not find embedded resource: {resourceName}", consoleOnly: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"Error loading image to button: {ex.Message}", consoleOnly: true);
            }
        }

        private void LoadImageToControl(System.Windows.Controls.Image imageControl, string resourceName)
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                using (var stream = assembly?.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        imageControl.Source = bitmap;
                        Logger.Instance.LogInfo($"Loaded image from embedded resource: {resourceName}", consoleOnly: true);
                    }
                    else
                    {
                        Logger.Instance.LogWarning($"Could not find embedded resource: {resourceName}", consoleOnly: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"Error loading image to control: {ex.Message}", consoleOnly: true);
            }
        }
    }

    public class InputDialog : Window
    {
        private System.Windows.Controls.TextBox txtInput;
        public string Answer { get; private set; } = string.Empty;

        public InputDialog(string title, string question)
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var label = new System.Windows.Controls.Label { Content = question, Margin = new Thickness(10, 10, 10, 0) };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);

            txtInput = new System.Windows.Controls.TextBox { Margin = new Thickness(10) };
            grid.Children.Add(txtInput);
            Grid.SetRow(txtInput, 1);

            var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(10) };

            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            okButton.Click += (s, e) => { Answer = txtInput.Text; DialogResult = true; };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75 };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            Content = grid;
            Loaded += (s, e) => txtInput.Focus();
        }
    }
}