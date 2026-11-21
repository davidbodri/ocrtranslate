using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Translumo.Services
{
    public class GlossaryService
    {
        private readonly string _glossaryPath;
        private readonly ConcurrentDictionary<string, string> _glossary = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger _logger;
        private DateTime _lastFileWriteTime;

        public GlossaryService(ILogger<GlossaryService> logger = null)
        {
            _logger = logger;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "Translumo");
            Directory.CreateDirectory(appDir);
            _glossaryPath = Path.Combine(appDir, "glossary.json");

            EnsureGlossaryFileExists();
            LoadGlossary();
        }

        private void EnsureGlossaryFileExists()
        {
            try
            {
                if (!File.Exists(_glossaryPath))
                {
                    var defaultEntries = new Dictionary<string, string>
                    {
                        { "Arthur", "Arthur" },
                        { "Dutch", "Dutch" },
                        { "John", "John" }
                    };
                    string json = JsonSerializer.Serialize(defaultEntries, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_glossaryPath, json);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create default glossary file");
            }
        }

        public void ReloadIfNeeded()
        {
            try
            {
                if (File.Exists(_glossaryPath))
                {
                    var lastWrite = File.GetLastWriteTimeUtc(_glossaryPath);
                    if (lastWrite != _lastFileWriteTime)
                    {
                        LoadGlossary();
                    }
                }
            }
            catch { }
        }

        private void LoadGlossary()
        {
            try
            {
                if (File.Exists(_glossaryPath))
                {
                    string json = File.ReadAllText(_glossaryPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    _glossary.Clear();
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Key))
                            {
                                _glossary[kvp.Key.Trim()] = kvp.Value?.Trim() ?? string.Empty;
                            }
                        }
                    }
                    _lastFileWriteTime = File.GetLastWriteTimeUtc(_glossaryPath);
                    _logger?.LogInformation("Loaded {Count} glossary entries", _glossary.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load glossary from {Path}", _glossaryPath);
            }
        }

        public string ApplyPostTranslation(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _glossary.IsEmpty)
            {
                return text;
            }

            ReloadIfNeeded();

            string result = text;
            foreach (var kvp in _glossary)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    continue;
                }

                // Match whole word or exact term
                string pattern = @"\b" + Regex.Escape(kvp.Key) + @"\b";
                result = Regex.Replace(result, pattern, kvp.Value, RegexOptions.IgnoreCase);
            }

            return result;
        }
    }
}
