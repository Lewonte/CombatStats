using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Runs;

namespace CombatStats.CombatStatsCode;

[HarmonyPatch]
public static class RunSummaryAndHistoryPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen._Ready))]
    private static void AddEndOfRunSummary(NGameOverScreen __instance)
    {
        RunStatsTracker.Instance.MarkCurrentRunCompleted();
        RunStatsRecord? record = RunStatsTracker.Instance.ActiveRun;
        if (record == null || __instance.GetNodeOrNull<RunStatsEndSummary>("CombatStatsEndSummary") != null)
        {
            return;
        }

        RunStatsEndSummary summary = new() { Name = "CombatStatsEndSummary" };
        __instance.AddChild(summary);
        summary.Build(record);
    }

    // DisplayRun is private in the base game. Harmony can still patch it by name without exposing game internals.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NRunHistory), "DisplayRun")]
    private static void AddStatsToVanillaHistory(NRunHistory __instance, RunHistory history)
    {
        RunStatsHistoryBadge? existing = __instance.GetNodeOrNull<RunStatsHistoryBadge>("CombatStatsHistoryBadge");
        existing?.QueueFree();

        RunStatsRecord? record = RunStatsStore.FindLatestCompletedRun(history.Seed);
        if (record == null)
        {
            return;
        }

        RunStatsHistoryBadge badge = new() { Name = "CombatStatsHistoryBadge" };
        __instance.AddChild(badge);
        badge.Build(record);
    }
}

public partial class RunStatsEndSummary : PanelContainer
{
    private VBoxContainer? _historyContainer;
    private Label? _historyDetails;
    private OptionButton? _historyPicker;
    private IReadOnlyList<RunStatsRecord> _history = [];

    public void Build(RunStatsRecord record)
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 40;
        OffsetTop = 150;
        OffsetRight = 650;
        OffsetBottom = 0;
        ZIndex = 200;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", RunStatsUiStyle.CreatePanelStyle());

        VBoxContainer root = new();
        AddChild(root);

        HBoxContainer header = new();
        root.AddChild(header);

        Label title = new()
        {
            Text = "COMBAT STATS - THIS RUN",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        header.AddChild(title);

        Button historyButton = new() { Text = "History" };
        historyButton.Pressed += ToggleHistory;
        header.AddChild(historyButton);

        Label subtitle = new()
        {
            Text = record.IsMultiplayer
                ? $"Your stats - {record.PlayerCount}-player run"
                : "Your stats - single-player run"
        };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(subtitle);

        Label stats = new()
        {
            Text = RunStatsText.FormatDetailed(record.Stats),
            CustomMinimumSize = new Vector2(435, 0)
        };
        stats.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(stats);

        if (record.IsMultiplayer && record.PlayerStats.Count > 1)
        {
            Label teamTitle = new() { Text = "TEAM COMPARISON" };
            teamTitle.AddThemeFontSizeOverride("font_size", 17);
            root.AddChild(teamTitle);

            Label team = new()
            {
                Text = RunStatsText.FormatTeamComparison(record),
                CustomMinimumSize = new Vector2(585, 0)
            };
            team.AddThemeFontSizeOverride("font_size", 15);
            root.AddChild(team);
        }

        _historyContainer = new VBoxContainer { Visible = false };
        root.AddChild(_historyContainer);

        Label historyTitle = new() { Text = "RECENT COMBATSTATS RUNS" };
        historyTitle.AddThemeFontSizeOverride("font_size", 17);
        _historyContainer.AddChild(historyTitle);

        _historyPicker = new OptionButton();
        _historyPicker.ItemSelected += index => ShowHistoryRecord((int)index);
        _historyContainer.AddChild(_historyPicker);

        _historyDetails = new Label { CustomMinimumSize = new Vector2(585, 0) };
        _historyDetails.AddThemeFontSizeOverride("font_size", 15);
        _historyContainer.AddChild(_historyDetails);
    }

    private void ToggleHistory()
    {
        if (_historyContainer == null)
        {
            return;
        }

        _historyContainer.Visible = !_historyContainer.Visible;
        if (!_historyContainer.Visible || _historyPicker == null)
        {
            return;
        }

        _history = RunStatsStore.GetCompletedRuns();
        _historyPicker.Clear();
        for (int index = 0; index < _history.Count; index++)
        {
            RunStatsRecord record = _history[index];
            string date = (record.CompletedAt ?? record.UpdatedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            string mode = record.IsMultiplayer ? $"{record.PlayerCount}P" : "Solo";
            _historyPicker.AddItem($"{date} - {mode} - {record.Stats.DamageDealt} damage");
        }

        if (_history.Count > 0)
        {
            ShowHistoryRecord(0);
        }
        else if (_historyDetails != null)
        {
            _historyDetails.Text = "No saved CombatStats runs yet.";
        }
    }

    private void ShowHistoryRecord(int index)
    {
        if (_historyDetails == null || index < 0 || index >= _history.Count)
        {
            return;
        }

        RunStatsRecord record = _history[index];
        string comparison = record.IsMultiplayer && record.PlayerStats.Count > 1
            ? $"\n\nTEAM COMPARISON\n{RunStatsText.FormatTeamComparison(record)}"
            : string.Empty;
        _historyDetails.Text = $"Seed: {record.Seed}\n\n{RunStatsText.FormatDetailed(record.Stats)}{comparison}";
    }
}

public partial class RunStatsHistoryBadge : PanelContainer
{
    public void Build(RunStatsRecord record)
    {
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -430;
        OffsetTop = 85;
        OffsetRight = -25;
        OffsetBottom = 0;
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", RunStatsUiStyle.CreatePanelStyle());

        Label label = new()
        {
            Text = $"COMBATSTATS\n{RunStatsText.FormatCompact(record.Stats)}",
            CustomMinimumSize = new Vector2(380, 0)
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        AddChild(label);
    }
}

public static class RunStatsText
{
    public static string FormatDetailed(RunStats stats)
    {
        string debuffs = stats.DebuffStacksById.Count == 0
            ? "none"
            : string.Join(", ", stats.DebuffStacksById
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Take(6)
                .Select(entry => $"{entry.Key} {entry.Value:0.#}"));

        return
            $"Combats: {stats.CombatsEntered}\n" +
            $"Damage dealt: {stats.DamageDealt}   Kills: {stats.EnemiesKilled}\n" +
            $"Enemy block removed: {stats.EnemyBlockRemoved}   Overkill: {stats.OverkillDamage}\n" +
            $"Block gained: {stats.BlockGained}   Damage prevented: {stats.BlockPrevented}\n" +
            $"Damage taken: {stats.DamageTaken}   Healing: {stats.HealingReceived}\n" +
            $"Cards - played {stats.CardsPlayed}, generated {stats.CardsGenerated}, drawn {stats.CardsDrawn}\n" +
            $"Cards - discarded {stats.CardsDiscarded}, exhausted {stats.CardsExhausted}\n" +
            $"Energy spent: {stats.EnergySpent}   Potions: {stats.PotionsUsed}\n" +
            $"Orbs channeled: {stats.OrbsChanneled}   Summons: {stats.SummonsCreated}\n" +
            $"Stars: +{stats.StarsGained} / -{stats.StarsSpent}\n" +
            $"Powers applied: {stats.PowerApplications} ({stats.PowerStacksApplied:0.#} stacks)\n" +
            $"Debuffs applied: {stats.DebuffApplications} ({stats.DebuffStacksApplied:0.#} stacks)\n" +
            $"Debuffs: {debuffs}";
    }

    public static string FormatCompact(RunStats stats) =>
        $"Damage: {stats.DamageDealt}   Kills: {stats.EnemiesKilled}\n" +
        $"Block: {stats.BlockGained}   Prevented: {stats.BlockPrevented}\n" +
        $"Cards played: {stats.CardsPlayed}   Healing: {stats.HealingReceived}\n" +
        $"Debuff stacks: {stats.DebuffStacksApplied:0.#}";

    public static string FormatTeamComparison(RunStatsRecord record)
    {
        List<PlayerRunStats> players = record.PlayerStats;
        if (players.Count == 0)
        {
            return "No player comparison was recorded for this run.";
        }

        int teamDamage = players.Sum(player => player.Stats.DamageDealt);
        int teamBlock = players.Sum(player => player.Stats.BlockGained);
        int teamTaken = players.Sum(player => player.Stats.DamageTaken);
        int teamHealing = players.Sum(player => player.Stats.HealingReceived);
        decimal teamDebuffs = players.Sum(player => player.Stats.DebuffStacksApplied);
        int teamCards = players.Sum(player => player.Stats.CardsPlayed);

        List<string> lines = [
            "Player                 Dmg   Block  Taken  Heal  Debuff  Cards"
        ];

        foreach (PlayerRunStats player in players)
        {
            RunStats stats = player.Stats;
            lines.Add($"{TrimName(player.DisplayName),-21} {stats.DamageDealt,4}  {stats.BlockGained,5}  {stats.DamageTaken,5}  {stats.HealingReceived,4}  {stats.DebuffStacksApplied,6:0.#}  {stats.CardsPlayed,5}");
        }

        lines.Add($"{"Team total",-21} {teamDamage,4}  {teamBlock,5}  {teamTaken,5}  {teamHealing,4}  {teamDebuffs,6:0.#}  {teamCards,5}");
        return string.Join("\n", lines);
    }

    private static string TrimName(string name) => name.Length <= 21 ? name : name[..20] + "…";
}

public static class RunStatsUiStyle
{
    public static StyleBoxFlat CreatePanelStyle() => new()
    {
        BgColor = new Color(0.02f, 0.03f, 0.06f, 0.92f),
        BorderColor = new Color(0.70f, 0.58f, 0.30f, 0.95f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomRight = 8,
        CornerRadiusBottomLeft = 8,
        ContentMarginLeft = 12,
        ContentMarginTop = 9,
        ContentMarginRight = 12,
        ContentMarginBottom = 9
    };
}
