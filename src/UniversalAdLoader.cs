using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Common;

namespace CSVGenerator
{
    /// <summary>
    /// Universal Ad Loader implementation that works across different platforms
    /// Implements the Common.IAdLoader interface
    /// </summary>
    public class UniversalAdLoader : IAdLoader
    {
        // Singleton instance
#if NET6_0_OR_GREATER
        private static UniversalAdLoader? _instance;
        public static UniversalAdLoader Instance => _instance ??= new UniversalAdLoader();
#else
        private static UniversalAdLoader _instance;
        public static UniversalAdLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UniversalAdLoader();
                }
                return _instance;
            }
        }
#endif

        // Network paths - multiple paths for redundancy
        private readonly List<string> _networkMetadataPaths = new List<string>
        {
            @"\\timnt757\Tools\NPI\Alex\FUI\ads\metadata.json",
            @"\\timnt779\MagicRay\Backup\Software programare\SW_FUI\fui\ads\metadata.json"
        };

        // Network paths for text ads
        private readonly List<string> _networkTextAdsPaths = new List<string>
        {
            @"\\timnt757\Tools\NPI\Alex\FUI\ads.txt",
            @"\\timnt779\MagicRay\Backup\Software programare\SW_FUI\fui\ads.txt"
        };

        // Local paths (only used as fallback)
        private readonly string _localMetadataPath = Path.Combine("assets", "ads", "metadata.json");
        private readonly string _localTextAdsPath = Path.Combine("assets", "ads", "ads.txt");

        // Callback for logging
#if NET6_0_OR_GREATER
        private Action<string, bool, bool, bool, bool, bool>? _logCallback;
#else
        private Action<string, bool, bool, bool, bool, bool> _logCallback;
#endif

        // Network timeout flag to avoid repeated timeouts
        private bool _networkTimeoutOccurred = false;
        private DateTime _lastNetworkAttempt = DateTime.MinValue;
        private readonly TimeSpan _timeoutResetInterval = TimeSpan.FromMinutes(5);
        private readonly int _networkTimeoutSeconds = 3;

        // Private constructor for singleton
        private UniversalAdLoader() { }

        // Cached metadata
#if NET6_0_OR_GREATER
        private Common.ImageAdMetadata? _cachedMetadata;
#else
        private Common.ImageAdMetadata _cachedMetadata;
#endif

        /// <summary>
        /// Initialize the loader with a logging callback
        /// </summary>
#if NET6_0_OR_GREATER
        public void Initialize(Action<string, bool, bool, bool, bool, bool>? logCallback)
#else
        public void Initialize(Action<string, bool, bool, bool, bool, bool> logCallback)
#endif
        {
            _logCallback = logCallback ?? ((msg, err, warn, succ, info, console) => { /* No-op if null */ });
        }

        /// <summary>
        /// Log a message using the callback if available
        /// </summary>
        private void Log(string message, bool consoleOnly = false)
        {
            _logCallback?.Invoke(message, false, false, false, true, consoleOnly);
        }

        /// <summary>
        /// Load ad metadata (both image and text ads) from metadata.json
        /// </summary>
#if NET6_0_OR_GREATER
        public async Task<Common.ImageAdMetadata> LoadAdMetadataAsync()
#else
        public Task<Common.ImageAdMetadata> LoadAdMetadataAsync()
#endif
        {
            try
            {
                // Create a merged metadata object
                var mergedMetadata = new Common.ImageAdMetadata();

                // Dictionary to track the latest version of each ad by ID
                var latestImageAds = new Dictionary<int, Common.ImageAd>();
                var latestTextAds = new Dictionary<int, Common.TextAd>();

                // Flag to track if we loaded from any network path
                bool loadedFromNetwork = false;

                // Check if we should reset the network timeout flag
                if (_networkTimeoutOccurred && DateTime.Now - _lastNetworkAttempt > _timeoutResetInterval)
                {
                    Log($"Resetting network timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                    _networkTimeoutOccurred = false;
                }

                // Try each network path in order
                if (!_networkTimeoutOccurred)
                {
                    // Update the last attempt time
                    _lastNetworkAttempt = DateTime.Now;
                    foreach (var networkPath in _networkMetadataPaths)
                    {
                        try
                        {
                            Log($"Attempting to load metadata from network path: {networkPath}", true);

#if NET6_0_OR_GREATER
                            // Create a cancellation token that will be used for the file read operation
                            using var cts = new System.Threading.CancellationTokenSource();
                            cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                            // For network file paths, use File.ReadAllTextAsync with the cancellation token
                            // This allows the operation to be properly cancelled if it takes too long
                            string json;
                            try
                            {
                                // Use ReadAllTextAsync with the cancellation token
                                json = await File.ReadAllTextAsync(networkPath, cts.Token);

                                // Deserialize the JSON
                                var metadata = JsonConvert.DeserializeObject<Common.ImageAdMetadata>(json, new JsonSerializerSettings
                                {
                                    NullValueHandling = NullValueHandling.Ignore
                                });

                                if (metadata != null)
                                {
                                    // Process image ads
                                    if (metadata.Images != null)
                                    {
                                        foreach (var imageAd in metadata.Images)
                                        {
                                            // Only add if this is a newer version of the ad
                                            if (!latestImageAds.ContainsKey(imageAd.Id) ||
                                                imageAd.Timestamp > latestImageAds[imageAd.Id].Timestamp)
                                            {
                                                latestImageAds[imageAd.Id] = imageAd;
                                            }
                                        }
                                    }

                                    // Process text ads
                                    if (metadata.Texts != null)
                                    {
                                        foreach (var textAd in metadata.Texts)
                                        {
                                            // Only add if this is a newer version of the ad
                                            if (!latestTextAds.ContainsKey(textAd.Id) ||
                                                textAd.Timestamp > latestTextAds[textAd.Id].Timestamp)
                                            {
                                                latestTextAds[textAd.Id] = textAd;
                                            }
                                        }
                                    }

                                    loadedFromNetwork = true;
                                    Log($"Successfully loaded metadata from {networkPath} with {metadata.Images?.Count ?? 0} images and {metadata.Texts?.Count ?? 0} text ads", true);
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                // Operation was cancelled due to timeout
                                Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);
                                Log("Operation was properly cancelled", true);
                            }
                            catch (OperationCanceledException)
                            {
                                // Operation was cancelled due to timeout
                                Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);
                                Log("Operation was properly cancelled", true);
                            }
#else
                            // Use a cancellation token source with a timeout
                            using (var cts = new System.Threading.CancellationTokenSource())
                            {
                                cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                                // For network file paths, use File.ReadAllText with a timeout
                                var readTask = Task.Run(() => File.ReadAllText(networkPath));
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_networkTimeoutSeconds), cts.Token);

                                // Wait for either the read task or the timeout task to complete
                                string json = null;
                                if (Task.WhenAny(readTask, timeoutTask).Result == readTask)
                                {
                                    // Read task completed first
                                    json = readTask.Result;

                                    // Deserialize the JSON
                                    var metadata = JsonConvert.DeserializeObject<Common.ImageAdMetadata>(json, new JsonSerializerSettings
                                    {
                                        NullValueHandling = NullValueHandling.Ignore
                                    });

                                    if (metadata != null)
                                    {
                                        // Process image ads
                                        if (metadata.Images != null)
                                        {
                                            foreach (var imageAd in metadata.Images)
                                            {
                                                // Only add if this is a newer version of the ad
                                                if (!latestImageAds.ContainsKey(imageAd.Id) ||
                                                    imageAd.Timestamp > latestImageAds[imageAd.Id].Timestamp)
                                                {
                                                    latestImageAds[imageAd.Id] = imageAd;
                                                }
                                            }
                                        }

                                        // Process text ads
                                        if (metadata.Texts != null)
                                        {
                                            foreach (var textAd in metadata.Texts)
                                            {
                                                // Only add if this is a newer version of the ad
                                                if (!latestTextAds.ContainsKey(textAd.Id) ||
                                                    textAd.Timestamp > latestTextAds[textAd.Id].Timestamp)
                                                {
                                                    latestTextAds[textAd.Id] = textAd;
                                                }
                                            }
                                        }

                                        loadedFromNetwork = true;
                                        Log($"Successfully loaded metadata from {networkPath} with {metadata.Images?.Count ?? 0} images and {metadata.Texts?.Count ?? 0} text ads", true);
                                    }
                                }
                                else
                                {
                                    // Timeout task completed first
                                    Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);

                                    // Cancel the read task to prevent it from continuing in the background
                                    try
                                    {
                                        // We can't directly cancel the File.ReadAllText task, but we can handle it
                                        // by ignoring its result when it eventually completes
                                        Log("Abandoning the network read operation to prevent hanging", true);
                                    }
                                    catch (Exception cancelEx)
                                    {
                                        Log($"Error handling timeout cancellation: {cancelEx.Message}", true);
                                    }
                                }
                            }
#endif
                        }
                        catch (Exception ex)
                        {
                            Log($"Failed to load metadata from network path {networkPath}: {ex.Message}", true);
                        }
                    }

                    // If all network paths failed, mark as timeout occurred
                    if (!loadedFromNetwork)
                    {
                        _networkTimeoutOccurred = true;
                        Log("All network paths failed, marking as timeout occurred", true);
                    }
                }
                else
                {
                    Log("Skipping network metadata load due to previous timeout", true);
                }

                // Try local file as fallback, but only if the directory already exists
#if NET6_0_OR_GREATER
                string? localDir = Path.GetDirectoryName(_localMetadataPath);
#else
                string localDir = Path.GetDirectoryName(_localMetadataPath);
#endif
                if (!loadedFromNetwork && !string.IsNullOrEmpty(localDir) && Directory.Exists(localDir) && File.Exists(_localMetadataPath))
                {
                    try
                    {
                        Log($"Loading metadata from local file: {_localMetadataPath}", true);
#if NET6_0_OR_GREATER
                        string json = await File.ReadAllTextAsync(_localMetadataPath);
#else
                        string json = File.ReadAllText(_localMetadataPath);
#endif

                        // Deserialize the JSON
                        var metadata = JsonConvert.DeserializeObject<Common.ImageAdMetadata>(json, new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });

                        if (metadata != null)
                        {
                            // Process image ads
                            if (metadata.Images != null)
                            {
                                foreach (var imageAd in metadata.Images)
                                {
                                    // Only add if this is a newer version of the ad
                                    if (!latestImageAds.ContainsKey(imageAd.Id) ||
                                        imageAd.Timestamp > latestImageAds[imageAd.Id].Timestamp)
                                    {
                                        latestImageAds[imageAd.Id] = imageAd;
                                    }
                                }
                            }

                            // Process text ads
                            if (metadata.Texts != null)
                            {
                                foreach (var textAd in metadata.Texts)
                                {
                                    // Only add if this is a newer version of the ad
                                    if (!latestTextAds.ContainsKey(textAd.Id) ||
                                        textAd.Timestamp > latestTextAds[textAd.Id].Timestamp)
                                    {
                                        latestTextAds[textAd.Id] = textAd;
                                    }
                                }
                            }

                            Log($"Successfully loaded metadata from local file with {metadata.Images?.Count ?? 0} images and {metadata.Texts?.Count ?? 0} text ads", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to load metadata from local file: {ex.Message}", true);
                    }
                }

                // Add all the latest ads to the merged metadata
                mergedMetadata.Images = new List<Common.ImageAd>();
                foreach (var ad in latestImageAds.Values)
                {
                    mergedMetadata.Images.Add(ad);
                }

                mergedMetadata.Texts = new List<Common.TextAd>();
                foreach (var ad in latestTextAds.Values)
                {
                    mergedMetadata.Texts.Add(ad);
                }

                Log($"Final merged metadata contains {mergedMetadata.Images.Count} images and {mergedMetadata.Texts.Count} text ads", true);

                // Cache the metadata for later use
                _cachedMetadata = mergedMetadata;

#if NET6_0_OR_GREATER
                return mergedMetadata;
#else
                return Task.FromResult(mergedMetadata);
#endif
            }
            catch (Exception ex)
            {
                Log($"Error loading ad metadata: {ex.Message}", true);
            }

            // Return empty metadata if loading failed
#if NET6_0_OR_GREATER
            return new Common.ImageAdMetadata();
#else
            return Task.FromResult(new Common.ImageAdMetadata());
#endif
        }

        /// <summary>
        /// Load text ads from ads.txt (legacy method)
        /// </summary>
#if NET6_0_OR_GREATER
        public async Task<List<string>> LoadTextAdsFromFileAsync()
#else
        public Task<List<string>> LoadTextAdsFromFileAsync()
#endif
        {
            var result = new List<string>();

            try
            {
                // First try to load from network paths
                bool loadedFromNetwork = false;

                // Check if we should reset the network timeout flag
                if (_networkTimeoutOccurred && DateTime.Now - _lastNetworkAttempt > _timeoutResetInterval)
                {
                    Log($"Resetting network timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                    _networkTimeoutOccurred = false;
                }

                if (!_networkTimeoutOccurred)
                {
                    // Update the last attempt time
                    _lastNetworkAttempt = DateTime.Now;
                    // Try each network path in order
                    foreach (var networkPath in _networkTextAdsPaths)
                    {
                        try
                        {
                            Log($"Attempting to load text ads from network path: {networkPath}", true);

#if NET6_0_OR_GREATER
                            // Create a cancellation token that will be used for the file read operation
                            using var cts = new System.Threading.CancellationTokenSource();
                            cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                            // For network file paths, use File.ReadAllTextAsync with the cancellation token
                            try
                            {
                                // Use ReadAllTextAsync with the cancellation token
                                string content = await File.ReadAllTextAsync(networkPath, cts.Token);

                                // Split the content into lines
                                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                result.AddRange(lines);

                                loadedFromNetwork = true;
                                Log($"Successfully loaded {lines.Length} text ads from network path: {networkPath}", true);

                                // We found one working network path, no need to try others
                                break;
                            }
                            catch (TaskCanceledException)
                            {
                                // Operation was cancelled due to timeout
                                Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);
                                Log("Operation was properly cancelled", true);
                            }
                            catch (OperationCanceledException)
                            {
                                // Operation was cancelled due to timeout
                                Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);
                                Log("Operation was properly cancelled", true);
                            }
#else
                            // Use a cancellation token source with a timeout
                            using (var cts = new System.Threading.CancellationTokenSource())
                            {
                                cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                                // For network file paths, use File.ReadAllText with a timeout
                                var readTask = Task.Run(() => File.ReadAllText(networkPath));
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_networkTimeoutSeconds), cts.Token);

                                // Wait for either the read task or the timeout task to complete
                                string content = null;
                                if (Task.WhenAny(readTask, timeoutTask).Result == readTask)
                                {
                                    // Read task completed first
                                    content = readTask.Result;

                                    // Split the content into lines
                                    string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                    result.AddRange(lines);

                                    loadedFromNetwork = true;
                                    Log($"Successfully loaded {lines.Length} text ads from network path: {networkPath}", true);

                                    // We found one working network path, no need to try others
                                    break;
                                }
                                else
                                {
                                    // Timeout task completed first
                                    Log($"Timeout loading from {networkPath} after {_networkTimeoutSeconds} seconds", true);

                                    // Cancel the read task to prevent it from continuing in the background
                                    try
                                    {
                                        // We can't directly cancel the File.ReadAllText task, but we can handle it
                                        // by ignoring its result when it eventually completes
                                        Log("Abandoning the network read operation to prevent hanging", true);
                                    }
                                    catch (Exception cancelEx)
                                    {
                                        Log($"Error handling timeout cancellation: {cancelEx.Message}", true);
                                    }
                                }
                            }
#endif
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("Cannot load a reference assembly for execution"))
                            {
                                // Create a list of default text ads
                                var textAds = new List<string>
                                {
                                    "Welcome to CSVGenerator - A utility to generate CSV files from BOM and PINS files",
                                    "Upload your BOM or PINS file to generate a CSV file",
                                    "Created by Adalbert Alexandru Ungureanu"
                                };

                                // Return the default text ads
#if NET6_0_OR_GREATER
                                return textAds;
#else
                                return Task.FromResult(textAds);
#endif
                            }
                            else
                            {
                                Log($"Failed to load text ads from network path {networkPath}: {ex.Message}", true);
                            }
                        }
                    }

                    // If all network paths failed, mark as timeout occurred
                    if (!loadedFromNetwork)
                    {
                        _networkTimeoutOccurred = true;
                        Log("All network paths failed, marking as timeout occurred", true);
                    }
                }
                else
                {
                    Log("Skipping network text ads load due to previous timeout", true);
                }

                // Try local file as fallback, but only if the file exists
                if (!loadedFromNetwork && File.Exists(_localTextAdsPath))
                {
                    try
                    {
                        Log($"Loading text ads from local file: {_localTextAdsPath}", true);
#if NET6_0_OR_GREATER
                        string[] lines = await File.ReadAllLinesAsync(_localTextAdsPath);
#else
                        string[] lines = File.ReadAllLines(_localTextAdsPath);
#endif
                        result.AddRange(lines);
                        Log($"Successfully loaded {lines.Length} text ads from local file", true);
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to load text ads from local file: {ex.Message}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading text ads: {ex.Message}", true);
            }

#if NET6_0_OR_GREATER
            return result;
#else
            return Task.FromResult(result);
#endif
        }

        /// <summary>
        /// Load an image file asynchronously
        /// </summary>
#if NET6_0_OR_GREATER
        public async Task<byte[]?> LoadImageFileAsync(string filename)
#else
        public async Task<byte[]> LoadImageFileAsync(string filename)
#endif
        {
            try
            {
                // Find the image file path
#if NET6_0_OR_GREATER
                string? filePath = await FindImageFileAsync(filename);
#else
                string filePath = await FindImageFileAsync(filename);
#endif
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    // Create a cancellation token to prevent UI freezing
#if NET6_0_OR_GREATER
                    using var cts = new System.Threading.CancellationTokenSource();
#else
                    using (var cts = new System.Threading.CancellationTokenSource())
#endif
                    {
                    cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                    try
                    {
                        // Load the file as bytes with cancellation token
#if NET6_0_OR_GREATER
                        return await File.ReadAllBytesAsync(filePath, cts.Token);
#else
                        // For .NET Framework, use Task.Run to make it cancellable
                        return await Task.Run(() => File.ReadAllBytes(filePath), cts.Token);
#endif
                    }
                    catch (TaskCanceledException)
                    {
                        Log($"Timeout loading image file {filename} after {_networkTimeoutSeconds} seconds", true);
                    }
                    catch (OperationCanceledException)
                    {
                        Log($"Operation cancelled when loading image file {filename}", true);
                    }
                }
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading image file {filename}: {ex.Message}", true);
            }

            return null;
        }

        /// <summary>
        /// Find an image file from the given filename asynchronously
        /// </summary>
#if NET6_0_OR_GREATER
        public async Task<string?> FindImageFileAsync(string fileName)
#else
        public Task<string> FindImageFileAsync(string fileName)
#endif
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Log("Image filename is empty", true);
#if NET6_0_OR_GREATER
                return null;
#else
                return Task.FromResult<string>(null);
#endif
            }

            // First check if the ads directory exists (without creating it)
            // Only check for local files if the directory already exists
            string adsDir = Path.Combine("assets", "ads");
            if (Directory.Exists(adsDir))
            {
                string localPath = Path.Combine(adsDir, fileName);
                if (File.Exists(localPath))
                {
                    Log($"Found image locally: {localPath}", true);
#if NET6_0_OR_GREATER
                    return Path.GetFullPath(localPath);
#else
                    return Task.FromResult(Path.GetFullPath(localPath));
#endif
                }
            }
            else
            {
                Log("Ads directory does not exist, skipping local file check", true);
            }

            // Check if we should reset the network timeout flag
            if (_networkTimeoutOccurred && DateTime.Now - _lastNetworkAttempt > _timeoutResetInterval)
            {
                Log($"Resetting network timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                _networkTimeoutOccurred = false;
            }

            // Check network paths for the image
            if (!_networkTimeoutOccurred)
            {
                // Update the last attempt time
                _lastNetworkAttempt = DateTime.Now;
                // Try each network path in order
                foreach (var basePath in _networkMetadataPaths)
                {
                    try
                    {
                        // Extract the base directory from the metadata path
#if NET6_0_OR_GREATER
                        string? baseDir = Path.GetDirectoryName(basePath);
#else
                        string baseDir = Path.GetDirectoryName(basePath);
#endif
                        if (string.IsNullOrEmpty(baseDir))
                        {
                            continue;
                        }

                        string networkPath = Path.Combine(baseDir, fileName);

#if NET6_0_OR_GREATER
                        // Use a cancellation token for the file check
                        using var cts = new System.Threading.CancellationTokenSource();
                        cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                        try
                        {
                            // Use FileInfo.Exists which is more efficient than File.Exists for network paths
                            // and wrap it in a Task.Run to make it cancellable
                            bool exists = await Task.Run(() => new FileInfo(networkPath).Exists, cts.Token);

                            if (exists)
                            {
                                Log($"Found image on network: {networkPath}", true);
                                return networkPath;
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            Log($"Timeout checking for image at {networkPath} after {_networkTimeoutSeconds} seconds", true);
                        }
                        catch (OperationCanceledException)
                        {
                            Log($"Operation cancelled when checking for image at {networkPath}", true);
                        }
                        catch (Exception fileEx)
                        {
                            Log($"Error checking for image at {networkPath}: {fileEx.Message}", true);
                        }
#else
                        // Use a task with timeout to check if the file exists
                        using (var cts = new System.Threading.CancellationTokenSource())
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                            try
                            {
                                // Run the file check in a separate task with timeout
                                var checkTask = Task.Run(() => File.Exists(networkPath));
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_networkTimeoutSeconds), cts.Token);

                                // Wait for either task to complete
                                if (Task.WhenAny(checkTask, timeoutTask).Result == checkTask && checkTask.Result)
                                {
                                    Log($"Found image on network: {networkPath}", true);
                                    return Task.FromResult(networkPath);
                                }
                                else if (timeoutTask.IsCompleted)
                                {
                                    Log($"Timeout checking for image at {networkPath}", true);
                                }
                            }
                            catch (Exception fileEx)
                            {
                                Log($"Error checking for image at {networkPath}: {fileEx.Message}", true);
                            }
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        Log($"Error accessing network path: {ex.Message}", true);
                        // Continue to the next path rather than giving up completely
                    }
                }

                // If we get here, all network paths failed
                _networkTimeoutOccurred = true;
                Log("All network paths failed when looking for image", true);
            }

            Log($"Image not found: {fileName}", true);
#if NET6_0_OR_GREATER
            return null;
#else
            return Task.FromResult<string>(null);
#endif
        }

        /// <summary>
        /// Get the cached metadata without loading it again
        /// </summary>
#if NET6_0_OR_GREATER
        public Common.ImageAdMetadata? GetCachedMetadata()
#else
        public Common.ImageAdMetadata GetCachedMetadata()
#endif
        {
            return _cachedMetadata;
        }

        /// <summary>
        /// Convert a timestamp to a human-readable string
        /// </summary>
        public string TimestampToString(long timestamp)
        {
            try
            {
                // Convert Unix timestamp to DateTime
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "Invalid timestamp";
            }
        }
    }
}
