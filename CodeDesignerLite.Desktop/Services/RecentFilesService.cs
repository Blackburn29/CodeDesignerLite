using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodeDesignerLite.Desktop.Services;

public class RecentFilesService
{
    private const int MaxRecentFiles = 10;
    private readonly string _recentFilesPath;
    private List<string> _recentFiles = new();

    public RecentFilesService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "CodeDesignerLite");
        Directory.CreateDirectory(appFolder);
        _recentFilesPath = Path.Combine(appFolder, "recent-files.json");
        LoadRecentFiles();
    }

    public IReadOnlyList<string> GetRecentFiles() => _recentFiles.AsReadOnly();

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        _recentFiles.Remove(filePath);
        _recentFiles.Insert(0, filePath);

        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();

        SaveRecentFiles();
    }

    public void ClearRecentFiles()
    {
        _recentFiles.Clear();
        SaveRecentFiles();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (File.Exists(_recentFilesPath))
            {
                var json = File.ReadAllText(_recentFilesPath);
                var files = JsonSerializer.Deserialize<List<string>>(json);
                _recentFiles = files?.Where(File.Exists).ToList() ?? new List<string>();
            }
        }
        catch
        {
            _recentFiles = new List<string>();
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_recentFilesPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
