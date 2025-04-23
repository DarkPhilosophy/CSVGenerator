using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#if NET6_0_OR_GREATER
#nullable enable
#else
#pragma warning disable CS8600, CS8602, CS8603, CS8604, CS8618, CS8625
#endif

namespace CSVGenerator
{
    public class FileParser
    {
#if NET6_0_OR_GREATER
        private static FileParser? _instance;
        private Action<string, bool, bool, bool, bool, bool>? _logCallback;
#else
        private static FileParser _instance;
        private Action<string, bool, bool, bool, bool, bool> _logCallback;
#endif

        private FileParser() { }

#if NET6_0_OR_GREATER
        public void Initialize(Action<string, bool, bool, bool, bool, bool>? logCallback)
#else
        public void Initialize(Action<string, bool, bool, bool, bool, bool> logCallback)
#endif
        {
            _logCallback = logCallback;
        }

#if NET6_0_OR_GREATER
        public static FileParser Instance => _instance ??= new FileParser();
#else
        public static FileParser Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FileParser();
                }
                return _instance;
            }
        }
#endif

        private void Log(string message, bool consoleOnly = true)
        {
            // Always write to console
            Console.WriteLine($"FileParser: {message}");

            // Use callback if available
            _logCallback?.Invoke($"FileParser: {message}", false, false, false, true, consoleOnly);
        }

#if NET6_0_OR_GREATER
        public List<BomData> ParseBomFile(string? filePath)
#else
        public List<BomData> ParseBomFile(string filePath)
#endif
        {
            var result = new List<BomData>();
            var invalidDevices = new HashSet<string> { "NOT_LOADED", "NOT_LOAD", "NO_LOADED", "NO_LOAD" };

            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Log($"BOM file not found: {filePath ?? "null"}");
                    return result;
                }

                string pattern = @"^\s*(\S+)\s*,\s*([\d\.\-]+),\s*([\d\.\-]+),\s*([\d\.\-]+),\s*(\S+)\s*,\s*\((.)\),\s*([\d\.\-]+),\s*(\S+),\s*'([^']*)',\s*'([^']*)';";
                var regex = new Regex(pattern, RegexOptions.Compiled);

                foreach (string line in File.ReadLines(filePath))
                {
                    string trimmedLine = line?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                    Match match = regex.Match(trimmedLine);
                    if (!match.Success)
                    {
                        Log($"Line not matched in BOM: {trimmedLine}");
                        continue;
                    }

                    string part = match.Groups[1].Value;
                    if (!double.TryParse(match.Groups[2].Value, out double x) ||
                        !double.TryParse(match.Groups[3].Value, out double y) ||
                        !double.TryParse(match.Groups[4].Value, out double rot))
                    {
                        Log($"Invalid numeric values in BOM line: {trimmedLine}");
                        continue;
                    }
                    string grid = match.Groups[5].Value;
                    string type = match.Groups[6].Value;
                    string size = match.Groups[7].Value;
                    string shp = match.Groups[8].Value;
                    string device = match.Groups[9].Value;
                    string outline = match.Groups[10].Value;

                    if (shp != "PTH" && shp != "RADIAL" || invalidDevices.Contains(device))
                    {
                        continue;
                    }

#if NET6_0_OR_GREATER
                    BomData? existingPart = result.Find(p => p.Part == part && p.Type == type);
#else
                    BomData existingPart = result.Find(p => p.Part == part && p.Type == type);
#endif
                    if (existingPart == null)
                    {
                        existingPart = new BomData { Part = part, Type = type, Data = new List<BomEntry>(), SeenData = new Dictionary<string, bool>() };
                        result.Add(existingPart);
                    }

                    string dataKey = $"{x}|{y}|{rot}";
                    if (!existingPart.SeenData.ContainsKey(dataKey))
                    {
                        existingPart.Data.Add(new BomEntry
                        {
                            X = x,
                            Y = y,
                            Rot = rot,
                            Grid = grid,
                            Shape = shp,
                            Device = device,
                            Outline = outline
                        });
                        existingPart.SeenData[dataKey] = true;
                    }
                }
                Log($"Parsed {result.Count} BOM parts with {result.Sum(p => p.Data.Count)} entries from {filePath}", false);
            }
            catch (Exception ex)
            {
                Log($"Error parsing BOM file: {ex.Message}");
            }
            return result;
        }

#if NET6_0_OR_GREATER
        public List<PinsData> ParsePinsFile(string? filePath)
#else
        public List<PinsData> ParsePinsFile(string filePath)
#endif
        {
            var result = new List<PinsData>();

            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Log($"PINS file not found: {filePath ?? "null"}");
                    return result;
                }

                string headerPattern = @"^Part\s+(\S+)\s+\((\w+)\)";
                string dataPattern1 = @"^\s*(\S+)\s+(\S+)\s+([\d\.\-]+)\s+([\d\.\-]+)\s+([\d\.\-]+)\s+(\S+)$";
                string dataPattern2 = @"^\s*""(\S+)"",""(\S+)"",""([\d\.\-]+)"",""([\d\.\-]+)"",""([\w]+)"",""(\S+)"","""",""""$";

                var headerRegex = new Regex(headerPattern, RegexOptions.Compiled);
                var dataRegex1 = new Regex(dataPattern1, RegexOptions.Compiled);
                var dataRegex2 = new Regex(dataPattern2, RegexOptions.Compiled);

#if NET6_0_OR_GREATER
                PinsData? currentPart = null;
#else
                PinsData currentPart = null;
#endif

                foreach (string line in File.ReadLines(filePath))
                {
                    string trimmedLine = line?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                    Match headerMatch = headerRegex.Match(trimmedLine);
                    if (headerMatch.Success)
                    {
                        string part = headerMatch.Groups[1].Value;
                        string type = headerMatch.Groups[2].Value;
                        currentPart = new PinsData { Part = part, Type = type, Data = new List<PinEntry>() };
                        result.Add(currentPart);
                        Log($"Parsed PINS header: Part={part}, Type={type}");
                        continue;
                    }

#if NET6_0_OR_GREATER
                    string? pin = null;
                    string? partName = null;
                    double x = 0;
                    double y = 0;
                    string? layer = null;
                    string? net = null;
#else
                    string pin = null;
                    string partName = null;
                    double x = 0;
                    double y = 0;
                    string layer = null;
                    string net = null;
#endif

                    Match dataMatch1 = dataRegex1.Match(trimmedLine);
                    if (dataMatch1.Success)
                    {
                        pin = dataMatch1.Groups[1].Value;
                        x = double.TryParse(dataMatch1.Groups[3].Value, out double xVal) ? xVal : 0;
                        y = double.TryParse(dataMatch1.Groups[4].Value, out double yVal) ? yVal : 0;
                        layer = dataMatch1.Groups[5].Value; // Treated as rotation, not layer
                        net = dataMatch1.Groups[6].Value;
                    }
                    else
                    {
                        Match dataMatch2 = dataRegex2.Match(trimmedLine);
                        if (dataMatch2.Success)
                        {
                            partName = dataMatch2.Groups[1].Value;
                            pin = dataMatch2.Groups[2].Value;
                            x = double.TryParse(dataMatch2.Groups[3].Value, out double xVal) ? xVal : 0;
                            y = double.TryParse(dataMatch2.Groups[4].Value, out double yVal) ? yVal : 0;
                            layer = dataMatch2.Groups[5].Value;
                            net = dataMatch2.Groups[6].Value;
                        }
                    }

                    if (pin == null || x == 0 || y == 0)
                    {
                        Log($"Line not matched or invalid in PINS: {trimmedLine}");
                        continue;
                    }

#if NET6_0_OR_GREATER
                    PinsData? targetPart = partName != null
                        ? result.Find(p => p.Part == partName) ?? new PinsData { Part = partName, Type = layer?[0].ToString() ?? "U", Data = new List<PinEntry>() }
                        : currentPart;
#else
                    PinsData targetPart = partName != null
                        ? result.Find(p => p.Part == partName) ?? new PinsData { Part = partName, Type = layer != null && layer.Length > 0 ? layer[0].ToString() : "U", Data = new List<PinEntry>() }
                        : currentPart;
#endif

                    if (targetPart == null)
                    {
                        Log($"No target part for PINS line: {trimmedLine}");
                        continue;
                    }

                    if (partName != null && !result.Contains(targetPart))
                    {
                        result.Add(targetPart);
                    }

                    // Set layer based on part Type (T -> 1, B -> 2)
                    int layerNum = targetPart.Type.ToUpper() == "T" ? 1 : targetPart.Type.ToUpper() == "B" ? 2 : 0;
                    if (layerNum == 0)
                    {
                        Log($"Invalid part type for PINS line: {trimmedLine}, Type={targetPart.Type}");
                        continue;
                    }

                    targetPart.Data.Add(new PinEntry
                    {
                        Pin = pin,
                        Name = pin,
                        X = x,
                        Y = y,
                        Layer = layerNum,
                        Net = net ?? string.Empty
                    });
                    Log($"Added PINS entry for Part={targetPart.Part}: Pin={pin}, X={x}, Y={y}, Layer={layerNum}, Net={net}");
                }
                Log($"Parsed {result.Count} PINS parts with {result.Sum(p => p.Data.Count)} pins from {filePath}", false);
            }
            catch (Exception ex)
            {
                Log($"Error parsing PINS file: {ex.Message}");
            }
            return result;
        }

#if NET6_0_OR_GREATER
        public string DetectUnit(string? filePath)
#else
        public string DetectUnit(string filePath)
#endif
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Log($"File not found for unit detection: {filePath ?? "null"}");
                    return "cm";
                }
                using (var reader = new StreamReader(filePath))
                {
                    string content = reader.ReadToEnd();
                    return content.IndexOf("inch", StringComparison.OrdinalIgnoreCase) >= 0 ? "inch" : "cm";
                }
            }
            catch (Exception ex)
            {
                Log($"Error detecting unit: {ex.Message}");
                return "cm";
            }
        }
    }

    public class BomData
    {
        public string Part { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<BomEntry> Data { get; set; } = new List<BomEntry>();
        public Dictionary<string, bool> SeenData { get; set; } = new Dictionary<string, bool>();
    }

    public class BomEntry
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Rot { get; set; }
        public string Grid { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string Outline { get; set; } = string.Empty;
    }

    public class PinsData
    {
        public string Part { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<PinEntry> Data { get; set; } = new List<PinEntry>();
    }

    public class PinEntry
    {
        public string Pin { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public int Layer { get; set; }
        public string Net { get; set; } = string.Empty;
    }
}