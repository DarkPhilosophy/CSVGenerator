using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Common.Logging;

namespace CSVGenerator.Core.Services
{
    /// <summary>
    /// Universal Ad Loader implementation that works across different platforms
    /// </summary>
    public class UniversalAdLoader
    {
        // Singleton instance
#if NET48
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
#else
        private static UniversalAdLoader? _instance;
        public static UniversalAdLoader Instance => _instance ??= new UniversalAdLoader();
#endif

        // GitHub URLs for ads
        private readonly string _githubLinkDataUrl = "https://raw.githubusercontent.com/DarkPhilosophy/Ads/master/linkdata.json";
        // Base URL for GitHub ads - can be updated from linkdata.json
        private string _githubAdsBaseUrl = "https://raw.githubusercontent.com/DarkPhilosophy/Ads/master/";

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
        private readonly string _localMetadataPath = Path.Combine("Ads", "metadata.json");
        private readonly string _localTextAdsPath = Path.Combine("Ads", "ads.txt");

        // GitHub paths
        private string _githubMetadataPath = "Ads/metadata.json";
        private string _githubTextAdsPath = "Ads/ads.txt";

        // Store GitHub metadata for direct access
        private Dictionary<string, object> _githubMetadata = null;
        private bool _githubTimeoutOccurred = false;
        private DateTime _lastGithubAttempt = DateTime.MinValue;

        // Callback for logging
        private Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>> _logCallback;

        // Network timeout flag to avoid repeated timeouts
        private bool _networkTimeoutOccurred = false;
        private DateTime _lastNetworkAttempt = DateTime.MinValue;
        private readonly TimeSpan _timeoutResetInterval = TimeSpan.FromMinutes(5);
        private readonly int _networkTimeoutSeconds = 3;

        // Private constructor for singleton
        private UniversalAdLoader() { }

        // Cached metadata
#if NET48
        private Dictionary<string, object> _cachedMetadata;
#else
        private Dictionary<string, object>? _cachedMetadata;
#endif

        /// <summary>
        /// Initialize the loader with a logging callback
        /// </summary>
        public void Initialize(Action<string, bool, bool, bool, bool, bool, Dictionary<string, object>> logCallback)
        {
            _logCallback = logCallback ?? ((msg, err, warn, succ, info, console, data) => { /* No-op if null */ });

            // Try to load linkdata.json from GitHub to update the ads base URL
            LoadLinkDataFromGitHub();
        }

        /// <summary>
        /// Load linkdata.json from GitHub to get the ads base URLs
        /// </summary>
        private async void LoadLinkDataFromGitHub()
        {
            try
            {
                Log($"Attempting to load linkdata.json from GitHub: {_githubLinkDataUrl}", true);

                // Log the current GitHub base URL for debugging
                Log($"Current GitHub base URL: {_githubAdsBaseUrl}", true);

                // Check if we should reset the GitHub timeout flag
                if (_githubTimeoutOccurred && DateTime.Now - _lastGithubAttempt > _timeoutResetInterval)
                {
                    Log($"Resetting GitHub timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                    _githubTimeoutOccurred = false;
                }

                if (!_githubTimeoutOccurred)
                {
                    _lastGithubAttempt = DateTime.Now;

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(_networkTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "CSVGenerator");

                        try
                        {
                            var response = await client.GetAsync(_githubLinkDataUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();
                                var linkData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                                if (linkData != null)
                                {
                                    // Clear existing network paths
                                    _networkMetadataPaths.Clear();
                                    _networkTextAdsPaths.Clear();

                                    // Create temporary lists to store the new paths
                                    var tempMetadataPaths = new List<string>();
                                    var tempTextAdsPaths = new List<string>();

                                    // Process all links in the linkdata.json file
                                    foreach (var entry in linkData)
                                    {
                                        string key = entry.Key;
                                        string value = entry.Value;

                                        if (!string.IsNullOrEmpty(value))
                                        {
                                            // If this is the primary GitHub link
                                            if (key == "linkdata")
                                            {
                                                // Force the correct GitHub URL regardless of what's in the file
                                                _githubAdsBaseUrl = "https://raw.githubusercontent.com/DarkPhilosophy/Ads/master/";
                                                Log($"Updated GitHub ads base URL to: {_githubAdsBaseUrl}", true);
                                            }
                                            // Add all links to the network paths for fallback
                                            if (value.StartsWith("\\\\"))
                                            {
                                                // This is a network path
                                                tempMetadataPaths.Add(Path.Combine(value, "ads", "metadata.json"));
                                                tempTextAdsPaths.Add(Path.Combine(value, "ads.txt"));
                                                Log($"Added network path from linkdata: {value}", true);
                                            }
                                        }
                                    }

                                    // Now update the actual lists
                                    _networkMetadataPaths.AddRange(tempMetadataPaths);
                                    _networkTextAdsPaths.AddRange(tempTextAdsPaths);

                                    // If no linkdata key was found, log a message
                                    if (!linkData.ContainsKey("linkdata"))
                                    {
                                        Log("linkdata.json does not contain 'linkdata' key", true);
                                    }

                                    // If no network paths were added, restore the defaults
                                    if (_networkMetadataPaths.Count == 0)
                                    {
                                        RestoreDefaultNetworkPaths();
                                    }
                                }
                                else
                                {
                                    Log("Failed to parse linkdata.json", true);
                                    RestoreDefaultNetworkPaths();
                                }
                            }
                            else
                            {
                                Log($"Failed to load linkdata.json from GitHub URL '{_githubLinkDataUrl}': {response.StatusCode}", true);
                                RestoreDefaultNetworkPaths();
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            Log($"Timeout loading linkdata.json from GitHub URL '{_githubLinkDataUrl}' after {_networkTimeoutSeconds} seconds", true);
                            _githubTimeoutOccurred = true;
                            RestoreDefaultNetworkPaths();
                        }
                        catch (Exception ex)
                        {
                            Log($"Error loading linkdata.json from GitHub URL '{_githubLinkDataUrl}': {ex.Message}", true);
                            RestoreDefaultNetworkPaths();
                        }
                    }
                }
                else
                {
                    Log("Skipping GitHub linkdata.json load due to previous timeout", true);
                    RestoreDefaultNetworkPaths();
                }
            }
            catch (Exception ex)
            {
                Log($"Error in LoadLinkDataFromGitHub: {ex.Message}", true);
                RestoreDefaultNetworkPaths();
            }
        }

        /// <summary>
        /// Restore the default network paths if loading from linkdata.json fails
        /// </summary>
        private void RestoreDefaultNetworkPaths()
        {
            _networkMetadataPaths.Clear();
            _networkTextAdsPaths.Clear();

            // Add default network paths
            _networkMetadataPaths.Add(@"\\timnt757\Tools\NPI\Alex\FUI\ads\metadata.json");
            _networkMetadataPaths.Add(@"\\timnt779\MagicRay\Backup\Software programare\SW_FUI\fui\ads\metadata.json");

            _networkTextAdsPaths.Add(@"\\timnt757\Tools\NPI\Alex\FUI\ads.txt");
            _networkTextAdsPaths.Add(@"\\timnt779\MagicRay\Backup\Software programare\SW_FUI\fui\ads.txt");

            Log("Restored default network paths", true);
        }

        /// <summary>
        /// Log a message using the callback if available
        /// </summary>
        private void Log(string message, bool consoleOnly = false)
        {
            _logCallback?.Invoke(message, false, false, false, true, consoleOnly, new Dictionary<string, object>());
        }

        /// <summary>
        /// Load ad metadata (both image and text ads) from metadata.json
        /// </summary>
#if NET48
        public Task<Dictionary<string, object>> LoadAdMetadataAsync()
#else
        public async Task<Dictionary<string, object>> LoadAdMetadataAsync()
#endif
        {
            try
            {
                // Create a merged metadata object
                var mergedMetadata = new Dictionary<string, object>();

                // Dictionary to track the latest version of each ad by ID
                var latestImageAds = new Dictionary<int, Dictionary<string, object>>();
                var latestTextAds = new Dictionary<int, Dictionary<string, object>>();

                // Flags to track if we loaded from any source
                bool loadedFromGitHub = false;
                bool loadedFromNetwork = false;

                // First try to load from GitHub
                if (!_githubTimeoutOccurred)
                {
                    // Check if we should reset the GitHub timeout flag
                    if (_githubTimeoutOccurred && DateTime.Now - _lastGithubAttempt > _timeoutResetInterval)
                    {
                        Log($"Resetting GitHub timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                        _githubTimeoutOccurred = false;
                    }

                    // Update the last attempt time
                    _lastGithubAttempt = DateTime.Now;

                    string githubMetadataUrl = $"{_githubAdsBaseUrl}{_githubMetadataPath}";
                    Log($"Attempting to load metadata from GitHub: {githubMetadataUrl}", true);

#if !NET48
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(_networkTimeoutSeconds);
                            client.DefaultRequestHeaders.Add("User-Agent", "CSVGenerator");

                            var response = await client.GetAsync(githubMetadataUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();
#else
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(_networkTimeoutSeconds);
                            client.DefaultRequestHeaders.Add("User-Agent", "CSVGenerator");

                            var response = client.GetAsync(githubMetadataUrl).Result;
                            if (response.IsSuccessStatusCode)
                            {
                                var json = response.Content.ReadAsStringAsync().Result;
#endif
                                var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, new JsonSerializerSettings
                                {
                                    NullValueHandling = NullValueHandling.Ignore
                                });

                                if (metadata != null)
                                {
                                    // Process image ads with case-insensitive key comparison
                                    Newtonsoft.Json.Linq.JArray imageArray = null;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "images" && metadata[key] is Newtonsoft.Json.Linq.JArray arr)
                                        {
                                            imageArray = arr;
                                            break;
                                        }
                                    }

                                    if (imageArray != null)
                                    {
                                        foreach (var item in imageArray)
                                        {
                                            if (item is Newtonsoft.Json.Linq.JObject imageObj)
                                            {
                                                var imageAd = imageObj.ToObject<Dictionary<string, object>>();
                                                if (imageAd != null && imageAd.ContainsKey("Id") && imageAd["Id"] is int id)
                                                {
                                                    // Check if we already have this ad and if this one is newer
                                                    if (!latestImageAds.ContainsKey(id) ||
                                                        (imageAd.ContainsKey("Timestamp") && imageAd["Timestamp"] is long timestamp &&
                                                         latestImageAds[id].ContainsKey("Timestamp") && latestImageAds[id]["Timestamp"] is long existingTimestamp &&
                                                         timestamp > existingTimestamp))
                                                    {
                                                        latestImageAds[id] = imageAd;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Process text ads with case-insensitive key comparison
                                    Newtonsoft.Json.Linq.JArray textArray = null;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "texts" && metadata[key] is Newtonsoft.Json.Linq.JArray arr)
                                        {
                                            textArray = arr;
                                            break;
                                        }
                                    }

                                    if (textArray != null)
                                    {
                                        foreach (var item in textArray)
                                        {
                                            if (item is Newtonsoft.Json.Linq.JObject textObj)
                                            {
                                                var textAd = textObj.ToObject<Dictionary<string, object>>();
                                                if (textAd != null && textAd.ContainsKey("Id") && textAd["Id"] is int id)
                                                {
                                                    // Check if we already have this ad and if this one is newer
                                                    if (!latestTextAds.ContainsKey(id) ||
                                                        (textAd.ContainsKey("Timestamp") && textAd["Timestamp"] is long timestamp &&
                                                         latestTextAds[id].ContainsKey("Timestamp") && latestTextAds[id]["Timestamp"] is long existingTimestamp &&
                                                         timestamp > existingTimestamp))
                                                    {
                                                        latestTextAds[id] = textAd;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    loadedFromGitHub = true;
                                    // Count images with case-insensitive key comparison
                                    int imageCount = 0;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "images" && metadata[key] is Newtonsoft.Json.Linq.JArray imgArr)
                                        {
                                            imageCount = imgArr.Count;
                                            break;
                                        }
                                    }

                                    // Count texts with case-insensitive key comparison
                                    int textCount = 0;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "texts" && metadata[key] is Newtonsoft.Json.Linq.JArray txtArr)
                                        {
                                            textCount = txtArr.Count;
                                            break;
                                        }
                                    }

                                    Log($"Successfully loaded metadata from GitHub with {imageCount} images and {textCount} text ads", true);

                                    // Store the GitHub metadata for direct access
                                    _githubMetadata = metadata;

                                    // Process the metadata directly into the merged metadata
                                    // We already processed the metadata above, no need to do it again

                                    // Log the full metadata content for debugging
                                    Log($"Metadata content: {JsonConvert.SerializeObject(metadata)}", true);
                                }
                            }
                            else
                            {
                                Log($"Failed to load metadata from GitHub: {response.StatusCode}", true);
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Log($"Timeout loading metadata from GitHub after {_networkTimeoutSeconds} seconds", true);
                        _githubTimeoutOccurred = true;
                    }
                    catch (Exception ex)
                    {
                        Log($"Error loading metadata from GitHub: {ex.Message}", true);
                    }
                }
                else
                {
                    Log("Skipping GitHub metadata load due to previous timeout", true);
                }

                // If GitHub failed, try network paths
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

#if !NET48
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
                                var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, new JsonSerializerSettings
                                {
                                    NullValueHandling = NullValueHandling.Ignore
                                });

                                if (metadata != null)
                                {
                                    // Process image ads with case-insensitive key comparison
                                    Newtonsoft.Json.Linq.JArray imageArray = null;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "images" && metadata[key] is Newtonsoft.Json.Linq.JArray arr)
                                        {
                                            imageArray = arr;
                                            break;
                                        }
                                    }

                                    if (imageArray != null)
                                    {
                                        foreach (var item in imageArray)
                                        {
                                            if (item is Newtonsoft.Json.Linq.JObject imageObj)
                                            {
                                                var imageAd = imageObj.ToObject<Dictionary<string, object>>();
                                                if (imageAd != null && imageAd.ContainsKey("Id") && imageAd["Id"] is int id)
                                                {
                                                    // Only add if this is a newer version of the ad
                                                    if (!latestImageAds.ContainsKey(id) ||
                                                        (imageAd.ContainsKey("Timestamp") &&
                                                         imageAd["Timestamp"] is DateTime timestamp &&
                                                         latestImageAds[id].ContainsKey("Timestamp") &&
                                                         latestImageAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                         timestamp > existingTimestamp))
                                                    {
                                                        latestImageAds[id] = imageAd;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Process text ads with case-insensitive key comparison
                                    Newtonsoft.Json.Linq.JArray textArray = null;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "texts" && metadata[key] is Newtonsoft.Json.Linq.JArray arr)
                                        {
                                            textArray = arr;
                                            break;
                                        }
                                    }

                                    if (textArray != null)
                                    {
                                        foreach (var item in textArray)
                                        {
                                            if (item is Newtonsoft.Json.Linq.JObject textObj)
                                            {
                                                var textAd = textObj.ToObject<Dictionary<string, object>>();
                                                if (textAd != null && textAd.ContainsKey("Id") && textAd["Id"] is int id)
                                                {
                                                    // Only add if this is a newer version of the ad
                                                    if (!latestTextAds.ContainsKey(id) ||
                                                        (textAd.ContainsKey("Timestamp") &&
                                                         textAd["Timestamp"] is DateTime timestamp &&
                                                         latestTextAds[id].ContainsKey("Timestamp") &&
                                                         latestTextAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                         timestamp > existingTimestamp))
                                                    {
                                                        latestTextAds[id] = textAd;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    loadedFromNetwork = true;

                                    // Count images with case-insensitive key comparison
                                    int imageCount = 0;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "images" && metadata[key] is Newtonsoft.Json.Linq.JArray imgArr)
                                        {
                                            imageCount = imgArr.Count;
                                            break;
                                        }
                                    }

                                    // Count texts with case-insensitive key comparison
                                    int textCount = 0;
                                    foreach (var key in metadata.Keys)
                                    {
                                        if (key.ToLower() == "texts" && metadata[key] is Newtonsoft.Json.Linq.JArray txtArr)
                                        {
                                            textCount = txtArr.Count;
                                            break;
                                        }
                                    }

                                    Log($"Successfully loaded metadata from {networkPath} with {imageCount} images and {textCount} text ads", true);
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
                                    var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, new JsonSerializerSettings
                                    {
                                        NullValueHandling = NullValueHandling.Ignore
                                    });

                                    if (metadata != null)
                                    {
                                        // Process image ads
                                        if (metadata.ContainsKey("Images") && metadata["Images"] is Newtonsoft.Json.Linq.JArray imageArray)
                                        {
                                            foreach (var item in imageArray)
                                            {
                                                if (item is Newtonsoft.Json.Linq.JObject imageObj)
                                                {
                                                    var imageAd = imageObj.ToObject<Dictionary<string, object>>();
                                                    if (imageAd != null && imageAd.ContainsKey("Id") && imageAd["Id"] is int id)
                                                    {
                                                        // Only add if this is a newer version of the ad
                                                        if (!latestImageAds.ContainsKey(id) ||
                                                            (imageAd.ContainsKey("Timestamp") &&
                                                             imageAd["Timestamp"] is DateTime timestamp &&
                                                             latestImageAds[id].ContainsKey("Timestamp") &&
                                                             latestImageAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                             timestamp > existingTimestamp))
                                                        {
                                                            latestImageAds[id] = imageAd;
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        // Process text ads
                                        if (metadata.ContainsKey("Texts") && metadata["Texts"] is Newtonsoft.Json.Linq.JArray textArray)
                                        {
                                            foreach (var item in textArray)
                                            {
                                                if (item is Newtonsoft.Json.Linq.JObject textObj)
                                                {
                                                    var textAd = textObj.ToObject<Dictionary<string, object>>();
                                                    if (textAd != null && textAd.ContainsKey("Id") && textAd["Id"] is int id)
                                                    {
                                                        // Only add if this is a newer version of the ad
                                                        if (!latestTextAds.ContainsKey(id) ||
                                                            (textAd.ContainsKey("Timestamp") &&
                                                             textAd["Timestamp"] is DateTime timestamp &&
                                                             latestTextAds[id].ContainsKey("Timestamp") &&
                                                             latestTextAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                             timestamp > existingTimestamp))
                                                        {
                                                            latestTextAds[id] = textAd;
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        loadedFromNetwork = true;
                                        int imageCount = metadata.ContainsKey("Images") && metadata["Images"] is Newtonsoft.Json.Linq.JArray imgArr ? imgArr.Count : 0;
                                        int textCount = metadata.ContainsKey("Texts") && metadata["Texts"] is Newtonsoft.Json.Linq.JArray txtArr ? txtArr.Count : 0;
                                        Log($"Successfully loaded metadata from {networkPath} with {imageCount} images and {textCount} text ads", true);
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

                    // We don't need to modify the collections here
                    // This was causing "Collection was modified; enumeration operation may not execute" errors
                }
                else
                {
                    Log("Skipping network metadata load due to previous timeout", true);
                }

                // Try local file as fallback, but only if the directory already exists and GitHub/network paths failed
#if !NET48
                string? localDir = Path.GetDirectoryName(_localMetadataPath);
#else
                string localDir = Path.GetDirectoryName(_localMetadataPath);
#endif
                if ((!loadedFromGitHub && !loadedFromNetwork) && !string.IsNullOrEmpty(localDir) && Directory.Exists(localDir) && File.Exists(_localMetadataPath))
                {
                    try
                    {
                        Log($"Loading metadata from local file: {_localMetadataPath}", true);
#if !NET48
                        string json = await File.ReadAllTextAsync(_localMetadataPath);
#else
                        string json = File.ReadAllText(_localMetadataPath);
#endif

                        // Deserialize the JSON
                        var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });

                        if (metadata != null)
                        {
                            // Process image ads
                            if (metadata.ContainsKey("Images") && metadata["Images"] is Newtonsoft.Json.Linq.JArray imageArray)
                            {
                                foreach (var item in imageArray)
                                {
                                    if (item is Newtonsoft.Json.Linq.JObject imageObj)
                                    {
                                        var imageAd = imageObj.ToObject<Dictionary<string, object>>();
                                        if (imageAd != null && imageAd.ContainsKey("Id") && imageAd["Id"] is int id)
                                        {
                                            // Only add if this is a newer version of the ad
                                            if (!latestImageAds.ContainsKey(id) ||
                                                (imageAd.ContainsKey("Timestamp") &&
                                                 imageAd["Timestamp"] is DateTime timestamp &&
                                                 latestImageAds[id].ContainsKey("Timestamp") &&
                                                 latestImageAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                 timestamp > existingTimestamp))
                                            {
                                                latestImageAds[id] = imageAd;
                                            }
                                        }
                                    }
                                }
                            }

                            // Process text ads
                            if (metadata.ContainsKey("Texts") && metadata["Texts"] is Newtonsoft.Json.Linq.JArray textArray)
                            {
                                foreach (var item in textArray)
                                {
                                    if (item is Newtonsoft.Json.Linq.JObject textObj)
                                    {
                                        var textAd = textObj.ToObject<Dictionary<string, object>>();
                                        if (textAd != null && textAd.ContainsKey("Id") && textAd["Id"] is int id)
                                        {
                                            // Only add if this is a newer version of the ad
                                            if (!latestTextAds.ContainsKey(id) ||
                                                (textAd.ContainsKey("Timestamp") &&
                                                 textAd["Timestamp"] is DateTime timestamp &&
                                                 latestTextAds[id].ContainsKey("Timestamp") &&
                                                 latestTextAds[id]["Timestamp"] is DateTime existingTimestamp &&
                                                 timestamp > existingTimestamp))
                                            {
                                                latestTextAds[id] = textAd;
                                            }
                                        }
                                    }
                                }
                            }

                            int imageCount = metadata.ContainsKey("Images") && metadata["Images"] is Newtonsoft.Json.Linq.JArray imgArr ? imgArr.Count : 0;
                            int textCount = metadata.ContainsKey("Texts") && metadata["Texts"] is Newtonsoft.Json.Linq.JArray txtArr ? txtArr.Count : 0;
                            Log($"Successfully loaded metadata from local file with {imageCount} images and {textCount} text ads", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to load metadata from local file: {ex.Message}", true);
                    }
                }

                // Add all the latest ads to the merged metadata
                var imagesList = new List<Dictionary<string, object>>();
                foreach (var ad in latestImageAds.Values)
                {
                    imagesList.Add(ad);
                }
                mergedMetadata["Images"] = imagesList;

                var textsList = new List<Dictionary<string, object>>();
                foreach (var ad in latestTextAds.Values)
                {
                    textsList.Add(ad);
                }
                mergedMetadata["Texts"] = textsList;

                Log($"Final merged metadata contains {imagesList.Count} images and {textsList.Count} text ads", true);

                // If we have no ads but we successfully loaded from GitHub, use the GitHub metadata directly
                if ((imagesList.Count == 0 && textsList.Count == 0) && loadedFromGitHub && _githubMetadata != null)
                {
                    Log("Using GitHub metadata directly since merged metadata is empty", true);
#if !NET48
                    return _githubMetadata;
#else
                    return Task.FromResult(_githubMetadata);
#endif
                }

                // Cache the metadata for later use
                _cachedMetadata = mergedMetadata;

#if !NET48
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
#if !NET48
            return new Dictionary<string, object>();
#else
            return Task.FromResult(new Dictionary<string, object>());
#endif
        }

        /// <summary>
        /// Load text ads from ads.txt (legacy method)
        /// </summary>
#if !NET48
        public async Task<List<string>> LoadTextAdsFromFileAsync()
#else
        public Task<List<string>> LoadTextAdsFromFileAsync()
#endif
        {
            var result = new List<string>();

            try
            {
                // First try to load from GitHub
                bool loadedFromGitHub = false;

                // Check if we should reset the GitHub timeout flag
                if (_githubTimeoutOccurred && DateTime.Now - _lastGithubAttempt > _timeoutResetInterval)
                {
                    Log($"Resetting GitHub timeout flag after {_timeoutResetInterval.TotalMinutes} minutes", true);
                    _githubTimeoutOccurred = false;
                }

                if (!_githubTimeoutOccurred)
                {
                    _lastGithubAttempt = DateTime.Now;

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(_networkTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "CSVGenerator");

                        string githubAdsUrl = $"{_githubAdsBaseUrl}{_githubTextAdsPath}";
                        Log($"Attempting to load text ads from GitHub: {githubAdsUrl}", true);

                        try
                        {
#if NET48
                            var response = client.GetAsync(githubAdsUrl).Result;
#else
                            var response = await client.GetAsync(githubAdsUrl);
#endif
                            if (response.IsSuccessStatusCode)
                            {
#if NET48
                                var content = response.Content.ReadAsStringAsync().Result;
#else
                                var content = await response.Content.ReadAsStringAsync();
#endif
                                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                result.AddRange(lines);

                                loadedFromGitHub = true;
                                Log($"Successfully loaded {lines.Length} text ads from GitHub", true);
                            }
                            else
                            {
                                Log($"Failed to load text ads from GitHub: {response.StatusCode}", true);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            Log($"Timeout loading text ads from GitHub after {_networkTimeoutSeconds} seconds", true);
                            _githubTimeoutOccurred = true;
                        }
                        catch (Exception ex)
                        {
                            Log($"Error loading text ads from GitHub: {ex.Message}", true);
                        }
                    }
                }
                else
                {
                    Log("Skipping GitHub text ads load due to previous timeout", true);
                }

                // If GitHub failed, try to load from network paths
                bool loadedFromNetwork = false;

                if (!loadedFromGitHub)
                {
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

#if !NET48
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
                                        "Welcome to ConfigReplacer - A utility to replace strings in configuration files",
                                        "Click the button to replace FFTesterBER with FFTesterSCH in your config files",
                                        "Created by Adalbert Alexandru Ungureanu"
                                    };

                                    // Return the default text ads
#if !NET48
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
                }

                // Try local file as fallback, but only if the file exists
                if (!loadedFromGitHub && !loadedFromNetwork && File.Exists(_localTextAdsPath))
                {
                    try
                    {
                        Log($"Loading text ads from local file: {_localTextAdsPath}", true);
#if !NET48
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

#if !NET48
            return result;
#else
            return Task.FromResult(result);
#endif
        }

        /// <summary>
        /// Load an image file asynchronously
        /// </summary>
#if !NET48
        public async Task<byte[]?> LoadImageFileAsync(string filename)
#else
        public async Task<byte[]> LoadImageFileAsync(string filename)
#endif
        {
            try
            {
                // Find the image file path
#if !NET48
                string? filePath = await FindImageFileAsync(filename);
#else
                string filePath = await FindImageFileAsync(filename);
#endif
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    // Create a cancellation token to prevent UI freezing
#if !NET48
                    using var cts = new System.Threading.CancellationTokenSource();
#else
                    using (var cts = new System.Threading.CancellationTokenSource())
#endif
                    {
                    cts.CancelAfter(TimeSpan.FromSeconds(_networkTimeoutSeconds));

                    try
                    {
                        // Load the file as bytes with cancellation token
#if !NET48
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
#if !NET48
        public async Task<string?> FindImageFileAsync(string fileName)
#else
        public Task<string> FindImageFileAsync(string fileName)
#endif
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Log("Image filename is empty", true);
#if !NET48
                return null;
#else
                return Task.FromResult<string>(null);
#endif
            }

            // First try to find the image on GitHub
            if (!_githubTimeoutOccurred)
            {
                _lastGithubAttempt = DateTime.Now;

                string githubImageUrl = $"{_githubAdsBaseUrl}Ads/{fileName}";
                Log($"Checking for image on GitHub: {githubImageUrl}", true);

                // Log the current state for debugging
                Log($"GitHub timeout occurred: {_githubTimeoutOccurred}, Network timeout occurred: {_networkTimeoutOccurred}", true);

                try
                {
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(_networkTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "CSVGenerator");

#if NET48
                        var response = client.GetAsync(githubImageUrl, HttpCompletionOption.ResponseHeadersRead).Result;
#else
                        var response = await client.GetAsync(githubImageUrl, HttpCompletionOption.ResponseHeadersRead);
#endif
                        if (response.IsSuccessStatusCode)
                        {
                            Log($"Found image on GitHub: {githubImageUrl}", true);

                            // Download the image to a temporary file
                            string tempDir = Path.Combine(Path.GetTempPath(), "CSVGenerator", "Ads");
                            Directory.CreateDirectory(tempDir);
                            string tempFile = Path.Combine(tempDir, fileName);

                            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
#if NET48
                                response.Content.CopyToAsync(fileStream).Wait();
#else
                                await response.Content.CopyToAsync(fileStream);
#endif
                            }

                            Log($"Downloaded image from GitHub to: {tempFile}", true);
#if !NET48
                            return tempFile;
#else
                            return Task.FromResult(tempFile);
#endif
                        }
                        else
                        {
                            Log($"Image not found on GitHub: {response.StatusCode}", true);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    Log($"Timeout checking for image on GitHub after {_networkTimeoutSeconds} seconds", true);
                    _githubTimeoutOccurred = true;
                }
                catch (Exception ex)
                {
                    Log($"Error checking for image on GitHub: {ex.Message}", true);
                }
            }
            else
            {
                Log("Skipping GitHub image check due to previous timeout", true);
            }

            // If GitHub failed, check if the ads directory exists locally (without creating it)
            // Only check for local files if the directory already exists
            string adsDir = "Ads";
            if (Directory.Exists(adsDir))
            {
                string localPath = Path.Combine(adsDir, fileName);
                if (File.Exists(localPath))
                {
                    Log($"Found image locally: {localPath}", true);
#if !NET48
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
#if !NET48
                        string? baseDir = Path.GetDirectoryName(basePath);
#else
                        string baseDir = Path.GetDirectoryName(basePath);
#endif
                        if (string.IsNullOrEmpty(baseDir))
                        {
                            continue;
                        }

                        string networkPath = Path.Combine(baseDir, fileName);

#if !NET48
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
#if !NET48
            return null;
#else
            return Task.FromResult<string>(null);
#endif
        }

        /// <summary>
        /// Get the cached metadata without loading it again
        /// </summary>
#if !NET48
        public Dictionary<string, object>? GetCachedMetadata()
#else
        public Dictionary<string, object> GetCachedMetadata()
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
