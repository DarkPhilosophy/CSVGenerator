using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Common;

#if NET6_0_OR_GREATER
#nullable enable
#else
#pragma warning disable CS8600, CS8602, CS8603, CS8604, CS8618, CS8625, CS8714
#endif

namespace CSVGenerator
{
    public partial class MainWindow : Window
    {
        private AppConfig _config = new AppConfig();
#if NET6_0_OR_GREATER
        private Window? _logWindow;
        private TextBox? _logWindowTextBox;
#else
        private Window _logWindow;
        private TextBox _logWindowTextBox;
#endif
        private StringBuilder _logBuffer = new StringBuilder();
        private bool _isLogWindowAttached = true;
        private Point _lastMainWindowPosition;
        private bool _loggerSubscribed = false;

        private static readonly SolidColorBrush BlueColor = Common.AnimationManager.BlueColor;
        private static readonly SolidColorBrush RedColor = Common.AnimationManager.RedColor;
        private static readonly SolidColorBrush YellowColor = Common.AnimationManager.YellowColor;
        private static readonly SolidColorBrush GrayColor = Common.AnimationManager.GrayColor;
        private static readonly SolidColorBrush GreenColor = Common.AnimationManager.GreenColor;

        private static readonly Dictionary<string, double> ConversionFactors = new Dictionary<string, double>
        {
            { "cm", 1.0 },
            { "inch", 25.4 }
        };

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            _config = AppConfig.Load();

            // Initialize FileParser with logging callback
            FileParser.Instance.Initialize(LogMessage);

            // Initialize the language manager with the saved language
            Common.LanguageManager.Instance.LoadLanguageFromConfig(_config.Language);

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

            // Initialize the ad banner and open log window
            this.Loaded += (s, e) => {
                // Pass both the text banner and the image container to AdManager
                // Also pass the UniversalAdLoader as the ad loader implementation
                Common.AdManager.Instance.Initialize(txtAdBanner, adContainer, LogMessage, UniversalAdLoader.Instance);

                // Set the initial language for the AdManager
                Common.AdManager.Instance.SwitchLanguage(_config.Language);

                // Set the initial flag image based on the current language
                string flagImage = _config.Language == "English" ? "pack://application:,,,/assets/Images/united-states.png" : "pack://application:,,,/assets/Images/romania.png";
                imgLanguageFlag.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(flagImage));

                // Don't create ads directory - only use it if it already exists
                // Make sure to use consoleOnly=true for ad-related messages
                Common.Logger.Instance.LogInfo("Using network paths for ads, local paths only as fallback if they exist", consoleOnly: true);

                // Set initial tooltip for the log button
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";

                // Open the log window by default
                CreateLogWindow();

                // Update button tooltip since log window is now open
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
            };

            LogMessage("Application started", consoleOnly: true);

            // Show welcome message
            string welcomeMessage = FindResource("ReadyToProcess")?.ToString() ?? "Ready to process files. Click one of the buttons to begin.";
            LogMessage(welcomeMessage, isInfo: true);
        }

        private void BtnSelectBomSplit_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = FindResource("OpenFileDialogTitle") as string ?? "Select File",
                Filter = FindResource("FileFilter") as string ?? "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtBomSplitPath.Text = openFileDialog.FileName;
                _config.LastBomSplitPath = openFileDialog.FileName;
                _config.Save();

                string unit = FileParser.Instance.DetectUnit(openFileDialog.FileName);
                btnBomSplitUnit.Content = unit;
                LogMessage($"BomSplit file selected: {Path.GetFileName(openFileDialog.FileName)}, unit: {unit}");
            }
        }

        private void BtnSelectCadPins_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = FindResource("OpenFileDialogTitle") as string ?? "Select File",
                Filter = FindResource("FileFilter") as string ?? "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtCadPinsPath.Text = openFileDialog.FileName;
                _config.LastCadPinsPath = openFileDialog.FileName;
                _config.Save();

                string unit = FileParser.Instance.DetectUnit(openFileDialog.FileName);
                btnCadPinsUnit.Content = unit;
                LogMessage($"CadPins file selected: {Path.GetFileName(openFileDialog.FileName)}, unit: {unit}");
            }
        }

        private void CmbClient_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isClientBeingDeleted)
            {
                return;
            }

            if (e.OriginalSource is ComboBox && Mouse.DirectlyOver is Button button &&
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
#if NET6_0_OR_GREATER
        private string? _clientBeingDeleted = null;

        private bool _isProgramBeingDeleted = false;
        private string? _programBeingDeleted = null;
#else
        private string _clientBeingDeleted = null;

        private bool _isProgramBeingDeleted = false;
        private string _programBeingDeleted = null;
#endif

        private void ClientDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string clientName)
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

                    Dispatcher.BeginInvoke(new Action(() => {
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
            string dropFileHere = FindResource("DropFileHere")?.ToString() ?? "Drop file here";
            if (string.IsNullOrEmpty(txtBomSplitPath.Text) || txtBomSplitPath.Text == dropFileHere || !File.Exists(txtBomSplitPath.Text))
            {
                string errorMessage = FindResource("SelectBomSplitFirst")?.ToString() ?? "Please select a BomSplit file first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                MessageBox.Show(
                    errorMessage,
                    errorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (cmbClient.SelectedItem == null)
            {
                string errorMessage = FindResource("SelectClientFirst")?.ToString() ?? "Please select a client first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                MessageBox.Show(
                    errorMessage,
                    errorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(cmbProgram.Text))
            {
                string errorMessage = FindResource("EnterProgramFirst")?.ToString() ?? "Please enter a program first.";
                string errorTitle = FindResource("Error")?.ToString() ?? "Error";
                MessageBox.Show(
                    errorMessage,
                    errorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

                string bomUnit = btnBomSplitUnit.Content?.ToString()?.ToLower() ?? "cm";
                string pinsUnit = btnCadPinsUnit.Content?.ToString()?.ToLower() ?? "cm";
                double bomFactor = ConversionFactors.ContainsKey(bomUnit) ? ConversionFactors[bomUnit] : 1.0;
                double pinsFactor = ConversionFactors.ContainsKey(pinsUnit) ? ConversionFactors[pinsUnit] : 1.0;

                // Move the program to the top of the history list if it exists, or add it if it doesn't
                if (_config.ProgramHistory.Contains(program))
                {
                    _config.ProgramHistory.Remove(program);
                }

                _config.ProgramHistory.Insert(0, program);

                // Limit history to 10 items
                if (_config.ProgramHistory.Count > 10)
                {
                    _config.ProgramHistory.RemoveAt(_config.ProgramHistory.Count - 1);
                }
                _config.Save();

                // Update the ComboBox items
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

                Common.AnimationManager.Instance.StartPulsingBorderAnimation(btnGenerate);

                string parsingBomMessage = FindResource("ParsingBomFile")?.ToString() ?? "Parsing BOM file...";
                UpdateProgress(10, parsingBomMessage);
                var bomData = FileParser.Instance.ParseBomFile(bomFilePath);
                if (bomData.Count == 0)
                {
                    throw new Exception("Failed to parse BOM file or no valid data found.");
                }
                string parsedBomMsg = string.Format(FindResource("ParsedBomFile")?.ToString() ?? "Parsed BOM file with {0} parts", bomData.Count);
                LogMessage(parsedBomMsg, true);

#if NET6_0_OR_GREATER
                List<PinsData>? pinsData = null;
#else
                List<PinsData> pinsData = null;
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
                        LogMessage(parsedPinsMsg, true);
                    }
                }
                // Create a custom progress callback that doesn't duplicate messages
                Action<int, string> customProgressCallback = (progress, message) => {
                    // Only update progress bar and status text, don't log to avoid duplication
                    // with the detailed logs from CSVGenerator
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
                    // Just update the progress bar and status text, don't log to avoid duplication
                    progressBar.Value = 100;
                    txtProgress.Text = completedMessage;
                    progressBar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                    string completedTitle = FindResource("Completed")?.ToString() ?? "Completed";
                    MessageBox.Show(
                        completedMessage,
                        completedTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    Common.SoundPlayer.PlayButtonClickSound();
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
                MessageBox.Show(
                    $"{processingFailedMessage}\n\n{ex.Message}",
                    errorMessage,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                Cursor = System.Windows.Input.Cursors.Arrow;
                Common.AnimationManager.Instance.StopPulsingBorderAnimation(btnGenerate);
            }
        }

        private void UpdateProgress(int value, string message)
        {
            progressBar.Value = value;
            txtProgress.Text = message;
            // Only log messages that are not generated by CSVGenerator
            // to avoid duplicate logging
            if (value == 0 || value == 10 || value == 30)
            {
                LogMessage(message);
            }
            progressBar.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        public void LogMessage(string message, bool isError = false, bool isWarning = false, bool isSuccess = false, bool isInfo = false, bool consoleOnly = false)
        {
            // Use the Common Logger
            Common.Logger.Instance.LogMessage(message, isError, isWarning, isSuccess, isInfo, consoleOnly);

            // Subscribe to the Logger's OnLogMessage event if we haven't already
            if (!_loggerSubscribed)
            {
                Common.Logger.Instance.OnLogMessage += (formattedMessage, error, warning, success, info, console) => {
                    if (!console)
                    {
                        // Add to log buffer - append to end to maintain chronological order
                        _logBuffer.Append(formattedMessage + Environment.NewLine);

                        // Limit buffer size to 100 lines
                        string bufferText = _logBuffer.ToString();
                        string[] lines = bufferText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 100)
                        {
                            _logBuffer.Clear();
                            // Keep the most recent 100 lines
                            _logBuffer.Append(string.Join(Environment.NewLine, lines.Skip(lines.Length - 100)) + Environment.NewLine);
                        }

                        // Update detached log window if it exists and is visible
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
            // Create a new window
            _logWindow = new Window
            {
                Title = FindResource("LogWindowTitle")?.ToString() ?? "Log Window",
                Width = 600,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,  // Don't show in taskbar since it's virtually attached
                Owner = this            // Set the main window as the owner
            };

            // Position the window to the right of the main window
            _logWindow.Left = this.Left + this.Width;
            _logWindow.Top = this.Top;
            _lastMainWindowPosition = new Point(this.Left, this.Top);

            // Create a TextBox for the log content
            _logWindowTextBox = new TextBox
            {
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                IsUndoEnabled = false,
                AcceptsReturn = true,
                AcceptsTab = true
            };

            // Create a context menu for the TextBox
            ContextMenu contextMenu = new ContextMenu();

            // Add a "Copy All" menu item
            MenuItem copyAllMenuItem = new MenuItem { Header = FindResource("CopyAll")?.ToString() ?? "Copy All" };
            copyAllMenuItem.Click += (s, args) =>
            {
                try
                {
                    if (_logWindowTextBox != null && !string.IsNullOrEmpty(_logWindowTextBox.Text))
                    {
                        string errorMessage;
                        if (SafeCopyToClipboard(_logWindowTextBox.Text, out errorMessage))
                        {
                            // Success message will be shown by the task if it succeeds
                            LogMessage(FindResource("CopiedToClipboard")?.ToString() ?? "Text copied to clipboard", isSuccess: true);
                        }
                        else if (!string.IsNullOrEmpty(errorMessage))
                        {
                            // Only show error if it's not the cooldown message
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

            // Add a "Copy Selected" menu item
            MenuItem copySelectedMenuItem = new MenuItem { Header = FindResource("CopySelected")?.ToString() ?? "Copy Selected" };
            copySelectedMenuItem.Click += (s, args) =>
            {
                try
                {
                    if (_logWindowTextBox != null && !string.IsNullOrEmpty(_logWindowTextBox.SelectedText))
                    {
                        string errorMessage;
                        if (SafeCopyToClipboard(_logWindowTextBox.SelectedText, out errorMessage))
                        {
                            // Success message will be shown by the task if it succeeds
                            LogMessage(FindResource("SelectionCopiedToClipboard")?.ToString() ?? "Selection copied to clipboard", isSuccess: true);
                        }
                        else if (!string.IsNullOrEmpty(errorMessage))
                        {
                            // Only show error if it's not the cooldown message
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

            // Add a "Clear" menu item
            MenuItem clearMenuItem = new MenuItem { Header = FindResource("ClearLog")?.ToString() ?? "Clear" };
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

            // Add a "Toggle Attachment" menu item
            MenuItem toggleAttachMenuItem = new MenuItem { Header = FindResource("DetachFromMainWindow")?.ToString() ?? "Detach from Main Window" };
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

            // Set the context menu
            _logWindowTextBox.ContextMenu = contextMenu;

            // Set the TextBox as the window content
            _logWindow.Content = _logWindowTextBox;

            // Handle the Closing event to prevent actual closing
            _logWindow.Closing += (s, args) =>
            {
                args.Cancel = true;
                _logWindow.Hide();
                LogMessage("Log window hidden");
            };

            // Handle the LocationChanged event to detect when the log window is moved
            _logWindow.LocationChanged += LogWindow_LocationChanged;

            // Display the current log buffer in the log window
            if (_logWindowTextBox != null)
            {
                _logWindowTextBox.Text = _logBuffer.ToString();
                _logWindowTextBox.ScrollToEnd();
            }

            // Show the log window
            _logWindow.Show();

            // Set up the main window's LocationChanged event to move the log window with it
            this.LocationChanged += MainWindow_LocationChanged;
        }

        // Flag to prevent location change handling during programmatic moves
        private bool _isAdjustingLogWindowPosition = false;

        // Timer to delay snap detection to prevent flickering
        private System.Windows.Threading.DispatcherTimer _snapTimer = new System.Windows.Threading.DispatcherTimer();
        private bool _shouldSnap = false;

#if NET6_0_OR_GREATER
        private void LogWindow_LocationChanged(object? sender, EventArgs e)
#else
        private void LogWindow_LocationChanged(object sender, EventArgs e)
#endif
        {
            if (_logWindow == null || _isAdjustingLogWindowPosition) return;

            // Calculate the distance between the log window and the main window
            double distanceX = Math.Abs((_logWindow.Left) - (this.Left + this.Width));
            double distanceY = Math.Abs(_logWindow.Top - this.Top);

            if (_isLogWindowAttached)
            {
                // If the log window is dragged away from the main window, detach it
                if (distanceX > 20 || distanceY > 20) // Original 20 pixel threshold
                {
                    _isLogWindowAttached = false;
                    LogMessage("Log window detached", true);

                    // Update the context menu item if it exists
                    UpdateAttachmentMenuItem(false);
                }
            }
            else
            {
                // If the log window is dragged close to the main window, prepare to re-attach it
                // Check if the log window is positioned near the right edge of the main window
                bool isNearRightEdge = distanceX < 20; // Original 20 pixel snap range
                bool isVerticallyAligned = distanceY < 20; // Original 20 pixel vertical snap range

                // Instead of immediately snapping, set a flag and use a timer
                if (isNearRightEdge && isVerticallyAligned && !_shouldSnap)
                {
                    _shouldSnap = true;

                    // Reset the timer
                    _snapTimer.Stop();
                    _snapTimer.Interval = TimeSpan.FromMilliseconds(200); // Reduced wait time to 200ms
                    _snapTimer.Tick += (s, args) => {
                        _snapTimer.Stop();
                        if (_shouldSnap)
                        {
                            SnapLogWindow();
                            _shouldSnap = false;

                            // Release the window from drag mode by simulating mouse release
                            if (_logWindow != null)
                            {
                                // Force the window to finish any drag operation
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

                // Position the window precisely at the right edge of the main window
                _logWindow.Left = this.Left + this.Width;
                _logWindow.Top = this.Top;
                _lastMainWindowPosition = new Point(this.Left, this.Top);

                // Create a small tolerance zone where the window stays attached
                // This prevents detachment from small mouse movements
                _logWindow.LocationChanged -= LogWindow_LocationChanged;
                _logWindow.LocationChanged += LogWindow_LocationChanged;

                LogMessage("Log window attached", true);

                // Update the context menu item if it exists
                UpdateAttachmentMenuItem(true);

                // Force the window to update its position
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
                    if (item is MenuItem menuItem &&
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

        // Track the last time we copied to clipboard to prevent spamming
        private DateTime _lastClipboardOperation = DateTime.MinValue;
        private const int CLIPBOARD_COOLDOWN_MS = 200; // 200ms cooldown between clipboard operations

        /// <summary>
        /// Safely copy text to clipboard with cooldown to prevent spamming
        /// </summary>
        private bool SafeCopyToClipboard(string text, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if we're within the cooldown period
            TimeSpan timeSinceLastCopy = DateTime.Now - _lastClipboardOperation;
            if (timeSinceLastCopy.TotalMilliseconds < CLIPBOARD_COOLDOWN_MS)
            {
                errorMessage = FindResource("WaitBeforeCopying")?.ToString() ?? "Please wait before copying again";
                return false;
            }

            if (string.IsNullOrEmpty(text))
            {
                return true; // Nothing to copy, so technically it succeeded
            }

            // Update the last operation time
            _lastClipboardOperation = DateTime.Now;

            // Start a background task to copy to clipboard
            Task.Run(() => CopyTextToClipboardSTA(text))
                .ContinueWith(task => {
                    string result = task.Result;
                    if (!string.IsNullOrEmpty(result))
                    {
                        // Log error on UI thread
                        Dispatcher.Invoke(() => {
                            LogMessage($"{FindResource("ClipboardError")?.ToString() ?? "Error copying to clipboard"}: {result}", isError: true);
                        });
                    }
                });

            return true; // We've started the operation, so return success
        }

        /// <summary>
        /// Copies text to clipboard using an STA thread
        /// </summary>
        private string CopyTextToClipboardSTA(string text)
        {
            string error = string.Empty;

            // Create a thread-safe clipboard operation
            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            });

            // Set the apartment state to STA which is required for clipboard operations
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(500); // Wait up to 500ms for the thread to complete

            return error;
        }

#if NET6_0_OR_GREATER
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
#else
        private void MainWindow_LocationChanged(object sender, EventArgs e)
#endif
        {
            if (_logWindow == null || !_isLogWindowAttached) return;

            // Calculate how much the main window has moved
            double deltaX = this.Left - _lastMainWindowPosition.X;
            double deltaY = this.Top - _lastMainWindowPosition.Y;

            // Move the log window by the same amount
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

            // Update the last position
            _lastMainWindowPosition = new Point(this.Left, this.Top);
        }

        private void ToggleLogWindowAttachment()
        {
            if (_logWindow == null) return;

            _isLogWindowAttached = !_isLogWindowAttached;

            if (_isLogWindowAttached)
            {
                // Re-attach the log window to the right of the main window
                SnapLogWindow();
            }
            else
            {
                LogMessage("Log window detached", true);
                // Update the context menu item
                UpdateAttachmentMenuItem(false);
            }
        }

        private void BtnShowLog_Click(object sender, RoutedEventArgs e)
        {
            if (_logWindow == null || !_logWindow.IsVisible)
            {
                // Create and show the log window if it doesn't exist or isn't visible
                if (_logWindow == null)
                {
                    CreateLogWindow();
                }
                else
                {
                    // Copy current log content to the detached window
                    if (_logWindowTextBox != null)
                    {
                        _logWindowTextBox.Clear();
                        _logWindowTextBox.Text = _logBuffer.ToString();
                        _logWindowTextBox.ScrollToEnd();
                    }

                    // If the window was previously attached, reposition it
                    if (_isLogWindowAttached)
                    {
                        SnapLogWindow();
                    }

                    _logWindow.Show();
                }

                LogMessage("Log window opened");

                // Update button tooltip to "Hide Log Window"
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
            }
            else
            {
                // Hide the log window
                _logWindow.Hide();
                LogMessage("Log window hidden");

                // Update button tooltip to "Show Log Window"
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
            }
        }

        public void ClearMainLog()
        {
            // Clear the log buffer
            _logBuffer.Clear();
        }

        private void BtnBomSplitUnit_Click(object sender, RoutedEventArgs e)
        {
            string currentUnit = btnBomSplitUnit.Content?.ToString()?.ToLower() ?? "cm";
            string newUnit = currentUnit == "cm" ? "inch" : "cm";
            btnBomSplitUnit.Content = newUnit;
            LogMessage($"BomSplit unit changed to: {newUnit}");
        }

        private void BtnCadPinsUnit_Click(object sender, RoutedEventArgs e)
        {
            string currentUnit = btnCadPinsUnit.Content?.ToString()?.ToLower() ?? "cm";
            string newUnit = currentUnit == "cm" ? "inch" : "cm";
            btnCadPinsUnit.Content = newUnit;
            LogMessage($"CadPins unit changed to: {newUnit}");
        }

        private void BtnLanguageSwitch_Click(object sender, RoutedEventArgs e)
        {
            Common.SoundPlayer.PlayButtonClickSound();
            string nextLanguage = Common.LanguageManager.Instance.GetNextLanguage(_config.Language);
            Common.LanguageManager.Instance.SwitchLanguage(nextLanguage);
            _config.Language = nextLanguage;
            _config.Save();

            // Also update the AdManager language
            Common.AdManager.Instance.SwitchLanguage(nextLanguage);

            // Update log window title if it exists
            if (_logWindow != null)
            {
                _logWindow.Title = FindResource("LogWindowTitle")?.ToString() ?? "Log Window";
            }

            // Update button tooltips based on log window visibility
            if (_logWindow != null && _logWindow.IsVisible)
            {
                btnShowLog.ToolTip = FindResource("HideLogWindow")?.ToString() ?? "Hide Log Window";
            }
            else
            {
                btnShowLog.ToolTip = FindResource("ShowLogWindow")?.ToString() ?? "Show Log Window";
            }

            // Update the language flag image
            string flagImage = nextLanguage == "English" ? "pack://application:,,,/assets/Images/united-states.png" : "pack://application:,,,/assets/Images/romania.png";
            imgLanguageFlag.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(flagImage));

            string langChangedMsg = FindResource("LanguageChanged")?.ToString() ?? $"Language changed to {nextLanguage}";
            LogMessage(string.Format(langChangedMsg, nextLanguage));
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Copy;
        }

        private void BomSplitPath_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
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

        private void CadPinsPath_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
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

            if (e.OriginalSource is ComboBox && Mouse.DirectlyOver is Button button &&
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
                }
            }
        }

        private void ProgramDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string programName)
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

                    LogMessage($"Removed program: {programName}");

                    e.Handled = true;

                    Dispatcher.BeginInvoke(new Action(() => {
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


    }

    public class InputDialog : Window
    {
        private TextBox txtInput;
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

            var label = new Label { Content = question, Margin = new Thickness(10, 10, 10, 0) };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);

            txtInput = new TextBox { Margin = new Thickness(10) };
            grid.Children.Add(txtInput);
            Grid.SetRow(txtInput, 1);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };

            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            okButton.Click += (s, e) => { Answer = txtInput.Text; DialogResult = true; };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button { Content = "Cancel", Width = 75 };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            Content = grid;

            Loaded += (s, e) => txtInput.Focus();
        }
    }
}