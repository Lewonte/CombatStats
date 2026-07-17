using System.Text.Json;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;

namespace CombatStats.CombatStatsCode;

/// <summary>
/// Mod-owned persistence. This intentionally stays outside STS2's run-save schema, so it is safe for both
/// single-player and multiplayer and does not participate in deterministic simulation.
/// </summary>
public static class RunStatsStore
{
    private const int MaxSavedRuns = 50;
    private static readonly object Gate = new();
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "CombatStats",
        "run-history.json");

    private static List<RunStatsRecord>? _records;

    public static RunStatsRecord OpenRun(RunState state)
    {
        lock (Gate)
        {
            List<RunStatsRecord> records = GetRecords();
            ulong localPlayerId = LocalContext.GetMe(state)?.NetId ?? 0UL;
            string baseKey = $"{state.Rng.Seed:X16}-{localPlayerId}";
            RunStatsRecord? resumableRun = records.LastOrDefault(record =>
                record.BaseKey == baseKey && !record.Completed);

            if (resumableRun != null)
            {
                return resumableRun;
            }

            RunStatsRecord record = new()
            {
                BaseKey = baseKey,
                Key = $"{baseKey}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Seed = state.Rng.StringSeed,
                LocalPlayerId = localPlayerId,
                PlayerCount = state.Players.Count,
                IsMultiplayer = state.Players.Count > 1,
                StartedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            records.Add(record);
            WriteRecords(records);
            return record;
        }
    }

    public static void Save(RunStatsRecord record)
    {
        lock (Gate)
        {
            List<RunStatsRecord> records = GetRecords();
            int index = records.FindIndex(existing => existing.Key == record.Key);
            if (index >= 0)
            {
                records[index] = record;
            }
            else
            {
                records.Add(record);
            }

            TrimCompletedRuns(records);
            WriteRecords(records);
        }
    }

    public static IReadOnlyList<RunStatsRecord> GetCompletedRuns()
    {
        lock (Gate)
        {
            return GetRecords()
                .Where(record => record.Completed)
                .OrderByDescending(record => record.CompletedAt ?? record.UpdatedAt)
                .ToList();
        }
    }

    public static RunStatsRecord? FindLatestCompletedRun(string seed)
    {
        lock (Gate)
        {
            return GetRecords()
                .Where(record => record.Completed && record.Seed == seed)
                .OrderByDescending(record => record.CompletedAt ?? record.UpdatedAt)
                .FirstOrDefault();
        }
    }

    private static List<RunStatsRecord> GetRecords()
    {
        if (_records != null)
        {
            return _records;
        }

        try
        {
            _records = File.Exists(HistoryPath)
                ? JsonSerializer.Deserialize<List<RunStatsRecord>>(File.ReadAllText(HistoryPath)) ?? []
                : [];
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Unable to load CombatStats run history: {exception.Message}");
            _records = [];
        }

        return _records;
    }

    private static void WriteRecords(List<RunStatsRecord> records)
    {
        try
        {
            string? directory = Path.GetDirectoryName(HistoryPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = HistoryPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, HistoryPath, overwrite: true);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Unable to save CombatStats run history: {exception.Message}");
        }
    }

    private static void TrimCompletedRuns(List<RunStatsRecord> records)
    {
        while (records.Count(record => record.Completed) > MaxSavedRuns)
        {
            RunStatsRecord? oldest = records
                .Where(record => record.Completed)
                .OrderBy(record => record.CompletedAt ?? record.UpdatedAt)
                .FirstOrDefault();
            if (oldest == null)
            {
                return;
            }
            records.Remove(oldest);
        }
    }
}

public sealed class RunStatsRecord
{
    public string Key { get; set; } = string.Empty;
    public string BaseKey { get; set; } = string.Empty;
    public string Seed { get; set; } = string.Empty;
    public ulong LocalPlayerId { get; set; }
    public int PlayerCount { get; set; }
    public bool IsMultiplayer { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public RunStats Stats { get; set; } = new();
    public List<PlayerRunStats> PlayerStats { get; set; } = [];
}
