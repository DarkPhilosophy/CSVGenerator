using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

#if NET6_0_OR_GREATER
#nullable enable
#else

#endif

namespace CSVGenerator
{
    public class GenCSV
    {
#if NET6_0_OR_GREATER
        private static GenCSV? _instance;
#else
        private static GenCSV _instance;
#endif

        private GenCSV() { }

#if NET6_0_OR_GREATER
        public static GenCSV Instance => _instance ??= new GenCSV();
#else
        public static GenCSV Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GenCSV();
                }
                return _instance;
            }
        }
#endif

#if NET6_0_OR_GREATER
        public Dictionary<string, bool> GenerateCSVFiles(
            List<BomData>? bomData,
            List<PinsData>? pinsData,
            string? client,
            string? partnumber,
            double bomFactor,
            double pinsFactor,
            Action<int, string>? progressCallback,
            Action<string, bool, bool, bool, bool, bool>? logCallback = null)
#else
        public Dictionary<string, bool> GenerateCSVFiles(
            List<BomData> bomData,
            List<PinsData> pinsData,
            string client,
            string partnumber,
            double bomFactor,
            double pinsFactor,
            Action<int, string> progressCallback,
            Action<string, bool, bool, bool, bool, bool> logCallback = null)
#endif
        {
            var result = new Dictionary<string, bool> { { "top", false }, { "bot", false } };

            try
            {
                if (string.IsNullOrEmpty(partnumber))
                {
                    progressCallback?.Invoke(0, "Error: Part number is required.");
                    logCallback?.Invoke("Error: Part number is required.", true, false, false, false, false);
                    return result;
                }

                if (bomData == null || bomData.Count == 0)
                {
                    progressCallback?.Invoke(0, "Error: No BOM data parsed.");
                    logCallback?.Invoke("Error: No BOM data parsed.", true, false, false, false, false);
                    return result;
                }

#if NET6_0_OR_GREATER
                client ??= "UNKNOWN_CLIENT";
#else
                client = client ?? "UNKNOWN_CLIENT";
#endif

                // Generate TOP-side CSV
                string topSideMsg = string.Format(System.Windows.Application.Current.FindResource("GeneratingTopSideCSV")?.ToString() ?? $"Generating TOP-side CSV for {0} BOM parts", bomData.Count);
                progressCallback?.Invoke(50, topSideMsg);
                logCallback?.Invoke(topSideMsg, false, true, false, false, false);
                var topLines = new Dictionary<string, List<string>> { { "T", new List<string>() }, { "B", new List<string>() } };

                foreach (var bomEntry in bomData)
                {
                    if (bomEntry == null || (bomEntry.Type != "T" && bomEntry.Type != "B"))
                    {
                        logCallback?.Invoke($"Skipping invalid BOM entry: Part={bomEntry?.Part ?? "null"}, Type={bomEntry?.Type ?? "null"}", true, false, false, false, false);
                        continue;
                    }

                    var lines = topLines[bomEntry.Type];

                    // Create a detailed log message for BOM part processing
                    var detailedLog = new System.Text.StringBuilder();
                    detailedLog.AppendLine($"Processing BOM part {bomEntry.Part} (Type: {bomEntry.Type}) with {bomEntry.Data.Count} entries");

                    // Add details for each entry
                    foreach (var data in bomEntry.Data)
                    {
                        string pn = $"{client}-{data.Device ?? "NO_CLIENT"}";
                        double x = data.X * bomFactor;
                        double y = data.Y * bomFactor;
                        double rot = data.Rot;
                        string part = bomEntry.Part ?? "MISSING_PART";
                        string line = $"{part},{x:F2},{y:F2},{rot:F2},{pn},{pn}";

                        // Add the entry details to the log
                        detailedLog.AppendLine($"    {line}");

                        // Add to the actual lines collection
                        lines.Add(line + "\n");
                    }

                    // Log the detailed message
                    logCallback?.Invoke(detailedLog.ToString().TrimEnd(), false, false, false, false, false);
                }

                // Write TOP-side CSV files
                foreach (var side in topLines.Keys)
                {
                    if (topLines[side].Count > 0)
                    {
                        string path = $"{partnumber}_faza{(side == "T" ? "1" : "2")}_TOP.csv";
                        try
                        {
                            string writingTopCsvMsg = string.Format(System.Windows.Application.Current.FindResource("WritingTopCsv")?.ToString() ?? $"Writing TOP CSV: {0} with {1} lines", path, topLines[side].Count);
                            logCallback?.Invoke(writingTopCsvMsg, false, true, false, false, false);
                            File.WriteAllLines(path, topLines[side]);
                            result["top"] = true;
                            string generatedTopMsg = string.Format(System.Windows.Application.Current.FindResource("GeneratedTopSideCSV")?.ToString() ?? $"Generated TOP-side CSV: {0} with {1} lines", path, topLines[side].Count);
                            progressCallback?.Invoke(70, generatedTopMsg);
                            // Don't log this message as it's redundant with the WritingTopCsv message
                        }
                        catch (Exception ex)
                        {
                            progressCallback?.Invoke(0, $"Error writing TOP CSV {path}: {ex.Message}");
                            logCallback?.Invoke($"Error writing TOP CSV {path}: {ex.Message}", true, false, false, false, false);
                        }
                    }
                    else
                    {
                        string noTopLinesMsg = string.Format(System.Windows.Application.Current.FindResource("NoTopCsvLines")?.ToString() ?? $"No TOP CSV lines for side {0}", side);
                        logCallback?.Invoke(noTopLinesMsg, false, true, false, false, false);
                    }
                }

                // Generate BOT-side CSV if PINS data is provided
                if (pinsData != null && pinsData.Count > 0)
                {
                    string botSideMsg = string.Format(System.Windows.Application.Current.FindResource("GeneratingBotSideCSV")?.ToString() ?? $"Generating BOT-side CSV for {0} PINS parts", pinsData.Count);
                    progressCallback?.Invoke(80, botSideMsg);
                    logCallback?.Invoke(botSideMsg, false, true, false, false, false);
                    var botLines = new Dictionary<string, List<string>> { { "T", new List<string>() }, { "B", new List<string>() } };

#if NET6_0_OR_GREATER
                    var pinLookup = pinsData.Where(p => p != null && p.Part != null)
                                            .ToDictionary(p => p!.Part!, p => p, StringComparer.OrdinalIgnoreCase);
#else
                    var pinLookup = pinsData.Where(p => p != null && p.Part != null)
                                            .ToDictionary(p => p.Part, p => p, StringComparer.OrdinalIgnoreCase);
#endif

                    int matchCount = 0;
                    foreach (var bomEntry in bomData)
                    {
                        if (bomEntry == null || bomEntry.Part == null)
                        {
                            logCallback?.Invoke($"Skipping invalid BOM entry: Part={bomEntry?.Part ?? "null"}, Type={bomEntry?.Type ?? "null"}", true, false, false, false, false);
                            continue;
                        }

                        if (!pinLookup.TryGetValue(bomEntry.Part, out var pins) || pins == null)
                        {
                            string noPinsDataMsg = string.Format(System.Windows.Application.Current.FindResource("NoPinsDataForBomPart")?.ToString() ?? $"No PINS data for BOM part {0} (Type: {1})", bomEntry.Part, bomEntry.Type);
                            logCallback?.Invoke(noPinsDataMsg, false, false, false, false, false);
                            continue;
                        }

                        // Normalize Type for comparison
                        string bomType = bomEntry.Type.ToUpper();
#if NET6_0_OR_GREATER
                        string? pinsType = pins.Type?.ToUpper();
#else
                        string pinsType = pins.Type != null ? pins.Type.ToUpper() : null;
#endif
                        if (bomType != pinsType)
                        {
                            logCallback?.Invoke($"Type mismatch for BOM part {bomEntry.Part}: BOM Type={bomEntry.Type}, PINS Type={pins.Type}", true, false, false, false, false);
                            continue;
                        }

                        matchCount++;

                        // Create a detailed log message for matched parts
                        var detailedLog = new System.Text.StringBuilder();
                        string matchedPartMsg = string.Format(System.Windows.Application.Current.FindResource("MatchedBomPart")?.ToString() ?? $"Matched BOM part {0} (Type: {1}) with PINS part (Type: {2}), BOM entries: {3}, PINS entries: {4}",
                            bomEntry.Part, bomEntry.Type, pins.Type, bomEntry.Data.Count, pins.Data.Count);
                        detailedLog.AppendLine(matchedPartMsg);

                        var lines = botLines[bomType];
                        var csvLines = new List<string>();

                        foreach (var bomItem in bomEntry.Data)
                        {
                            foreach (var pinsItem in pins.Data)
                            {
                                if (pinsItem == null || pinsItem.X == 0 || pinsItem.Y == 0)
                                {
                                    logCallback?.Invoke($"Skipping invalid PINS entry for part {bomEntry.Part}: Pin={pinsItem?.Pin ?? "null"}, X={pinsItem?.X ?? 0}, Y={pinsItem?.Y ?? 0}", true, false, false, false, false);
                                    continue;
                                }
                                string pn = $"{client}-{bomItem.Device ?? "NO_CLIENT"}";
                                double x = pinsItem.X * pinsFactor;
                                double y = pinsItem.Y * pinsFactor;
                                string part = bomEntry.Part;
                                string pin = pinsItem.Pin ?? "X";
                                string line = $"{part}.{pin},{x:F2},{y:F2},0,{pn},THD";

                                // Add to the formatted log
                                detailedLog.AppendLine($"    {line}");

                                // Add to the actual lines collection
                                lines.Add(line + "\n");
                                csvLines.Add(line);
                            }
                        }

                        // Log the detailed message
                        logCallback?.Invoke(detailedLog.ToString().TrimEnd(), false, false, false, false, false);
                    }
                    string foundMatchingPartsMsg = string.Format(System.Windows.Application.Current.FindResource("FoundMatchingParts")?.ToString() ?? $"Found {0} matching parts for BOT CSV", matchCount);
                    logCallback?.Invoke(foundMatchingPartsMsg, false, true, false, false, false);

                    foreach (var side in botLines.Keys)
                    {
                        if (botLines[side].Count > 0)
                        {
                            string path = $"{partnumber}_faza{(side == "T" ? "1" : "2")}_BOT.csv";
                            try
                            {
                                string writingBotCsvMsg = string.Format(System.Windows.Application.Current.FindResource("WritingBotCsv")?.ToString() ?? $"Writing BOT CSV: {0} with {1} lines", path, botLines[side].Count);
                                logCallback?.Invoke(writingBotCsvMsg, false, true, false, false, false);
                                File.WriteAllLines(path, botLines[side]);
                                result["bot"] = true;
                                string generatedBotMsg = string.Format(System.Windows.Application.Current.FindResource("GeneratedBotSideCSV")?.ToString() ?? $"Generated BOT-side CSV: {0} with {1} lines", path, botLines[side].Count);
                                progressCallback?.Invoke(90, generatedBotMsg);
                                // Don't log this message as it's redundant with the WritingBotCsv message
                            }
                            catch (Exception ex)
                            {
                                progressCallback?.Invoke(0, $"Error writing BOT CSV {path}: {ex.Message}");
                                logCallback?.Invoke($"Error writing BOT CSV {path}: {ex.Message}", true, false, false, false, false);
                            }
                        }
                        else
                        {
                            string noBotLinesMsg = string.Format(System.Windows.Application.Current.FindResource("NoBotCsvLines")?.ToString() ?? $"No BOT CSV lines for side {0}", side);
                            logCallback?.Invoke(noBotLinesMsg, false, true, false, false, false);
                        }
                    }
                }
                else
                {
                    logCallback?.Invoke("No PINS data provided; skipping BOT CSV generation", false, true, false, false, false);
                }

                if (result["top"] || result["bot"])
                {
                    string completedMsg = System.Windows.Application.Current.FindResource("ProcessingCompletedSuccessfully")?.ToString() ?? "Processing completed successfully";
                    progressCallback?.Invoke(100, completedMsg);
                    logCallback?.Invoke(completedMsg, false, false, true, false, false);
                }
                else
                {
                    progressCallback?.Invoke(100, "Processing completed with no CSV files generated");
                    logCallback?.Invoke("Processing completed with no CSV files generated", false, true, false, false, false);
                }
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0, $"Error generating CSV files: {ex.Message}");
                logCallback?.Invoke($"Error generating CSV files: {ex.Message}", true, false, false, false, false);
            }

            return result;
        }
    }
}
