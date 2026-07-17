using System.Text.Json;

namespace CombatStats.CombatStatsCode;

public enum StatsSection
{
    Damage,
    Defense,
    Cards,
    Resources,
    Powers,
    Debuffs
}

/// <summary>
/// Persistent, mod-owned display preferences. The mod keeps this separate from game state and saves.
/// </summary>
public sealed class CombatStatsSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "CombatStats",
        "settings.json");

    public static CombatStatsSettings Instance { get; } = new();

    private SettingsData _data = new();

    public event Action? Changed;

    public bool HudVisible => _data.HudVisible;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                _data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath)) ?? new SettingsData();
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Unable to load CombatStats settings: {exception.Message}");
            _data = new SettingsData();
        }
    }

    public bool IsSectionVisible(StatsSection section) => section switch
    {
        StatsSection.Damage => _data.ShowDamage,
        StatsSection.Defense => _data.ShowDefense,
        StatsSection.Cards => _data.ShowCards,
        StatsSection.Resources => _data.ShowResources,
        StatsSection.Powers => _data.ShowPowers,
        StatsSection.Debuffs => _data.ShowDebuffs,
        _ => false
    };

    public void SetSectionVisible(StatsSection section, bool visible)
    {
        switch (section)
        {
            case StatsSection.Damage:
                _data.ShowDamage = visible;
                break;
            case StatsSection.Defense:
                _data.ShowDefense = visible;
                break;
            case StatsSection.Cards:
                _data.ShowCards = visible;
                break;
            case StatsSection.Resources:
                _data.ShowResources = visible;
                break;
            case StatsSection.Powers:
                _data.ShowPowers = visible;
                break;
            case StatsSection.Debuffs:
                _data.ShowDebuffs = visible;
                break;
            default:
                return;
        }

        SaveAndNotify();
    }

    public void ToggleHudVisibility()
    {
        _data.HudVisible = !_data.HudVisible;
        SaveAndNotify();
    }

    private void SaveAndNotify()
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Unable to save CombatStats settings: {exception.Message}");
        }

        Changed?.Invoke();
    }

    private sealed class SettingsData
    {
        public bool HudVisible { get; set; } = true;
        public bool ShowDamage { get; set; } = true;
        public bool ShowDefense { get; set; } = true;
        public bool ShowCards { get; set; } = true;
        public bool ShowResources { get; set; } = true;
        public bool ShowPowers { get; set; } = true;
        public bool ShowDebuffs { get; set; } = true;
    }
}
