using System.IO;
using System.Text.Json;
using LoqLighting.App.Models;

namespace LoqLighting.App.Services;

public class ProfileService
{
    private readonly string _profilesDirectory;
    private readonly string _lastUsedPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ProfileService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDir = Path.Combine(appData, "LoqLightingStudio");
        _profilesDirectory = Path.Combine(baseDir, "profiles");
        _lastUsedPath = Path.Combine(baseDir, "last_used.txt");

        Directory.CreateDirectory(_profilesDirectory);
    }

    public IReadOnlyList<LightingProfile> LoadAllProfiles()
    {
        var profiles = new List<LightingProfile>();
        if (!Directory.Exists(_profilesDirectory))
        {
            return profiles;
        }

        foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<LightingProfile>(json);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Skip corrupted profile file
            }
        }

        return profiles;
    }

    public void SaveProfile(LightingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profile));
        }

        Directory.CreateDirectory(_profilesDirectory);
        string safeFileName = string.Join("_", profile.Name.Split(Path.GetInvalidFileNameChars())) + ".json";
        string filePath = Path.Combine(_profilesDirectory, safeFileName);

        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(filePath, json);

        SaveLastUsedProfileName(profile.Name);
    }

    public bool DeleteProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return false;

        string safeFileName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars())) + ".json";
        string filePath = Path.Combine(_profilesDirectory, safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }

        return false;
    }

    public void SaveLastUsedProfileName(string name)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_lastUsedPath)!);
            File.WriteAllText(_lastUsedPath, name);
        }
        catch
        {
            // Ignore failure to remember last used
        }
    }

    public string? GetLastUsedProfileName()
    {
        try
        {
            if (File.Exists(_lastUsedPath))
            {
                return File.ReadAllText(_lastUsedPath).Trim();
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }
}
