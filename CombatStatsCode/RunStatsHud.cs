using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CombatStats.CombatStatsCode;

[HarmonyPatch]
public static class CombatUiPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi._Ready))]
    private static void AddRunStatsHud(NCombatUi __instance)
    {
        if (__instance.GetNodeOrNull<RunStatsHud>("CombatStatsHud") != null)
        {
            return;
        }

        RunStatsTracker.Instance.StartCombat();
        RunStatsHud hud = new() { Name = "CombatStatsHud" };
        __instance.AddChild(hud);
        hud.Bind();
    }

}

public partial class RunStatsHud : PanelContainer
{
    private Label? _statsLabel;
    private VBoxContainer? _settingsContainer;
    private Button? _settingsButton;
    private bool _settingsOpen;
    private bool _f8Held;

    public void Bind()
    {
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -430;
        OffsetTop = 145;
        OffsetRight = -24;
        OffsetBottom = 0;
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 100;
        SetProcess(true);

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.03f, 0.06f, 0.90f),
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
        });

        VBoxContainer root = new();
        AddChild(root);

        HBoxContainer header = new();
        root.AddChild(header);

        Label title = new()
        {
            Text = "RUN STATS",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(title);

        _settingsButton = new Button { Text = "Settings" };
        _settingsButton.Pressed += ToggleSettings;
        header.AddChild(_settingsButton);

        _statsLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0)
        };
        _statsLabel.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(_statsLabel);

        _settingsContainer = new VBoxContainer { Visible = false };
        root.AddChild(_settingsContainer);
        AddSectionToggle(StatsSection.Damage, "Damage and kills");
        AddSectionToggle(StatsSection.Defense, "Block, damage taken, and healing");
        AddSectionToggle(StatsSection.Cards, "Card activity");
        AddSectionToggle(StatsSection.Resources, "Resources, orbs, summons, and stars");
        AddSectionToggle(StatsSection.Powers, "All powers applied");
        AddSectionToggle(StatsSection.Debuffs, "Debuff totals and breakdown");

        Label hint = new() { Text = "F8 hides/shows this panel." };
        hint.AddThemeFontSizeOverride("font_size", 13);
        _settingsContainer.AddChild(hint);

        RunStatsTracker.Instance.Changed += Refresh;
        CombatStatsSettings.Instance.Changed += ApplySettings;
        ApplySettings();
        Refresh();
    }

    public override void _ExitTree()
    {
        RunStatsTracker.Instance.Changed -= Refresh;
        CombatStatsSettings.Instance.Changed -= ApplySettings;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        // Poll the key rather than relying on an NCombatUi input callback. Godot UI controls may consume
        // that callback before Harmony sees it, while a processing node continues working when hidden.
        bool f8Pressed = Input.IsKeyPressed(Key.F8);
        if (f8Pressed && !_f8Held)
        {
            CombatStatsSettings.Instance.ToggleHudVisibility();
        }

        _f8Held = f8Pressed;
    }

    private void AddSectionToggle(StatsSection section, string text)
    {
        CheckButton toggle = new()
        {
            Text = text,
            ButtonPressed = CombatStatsSettings.Instance.IsSectionVisible(section)
        };
        toggle.Toggled += isVisible => CombatStatsSettings.Instance.SetSectionVisible(section, isVisible);
        _settingsContainer?.AddChild(toggle);
    }

    private void ToggleSettings()
    {
        _settingsOpen = !_settingsOpen;
        if (_settingsContainer != null)
        {
            _settingsContainer.Visible = _settingsOpen;
        }
        if (_settingsButton != null)
        {
            _settingsButton.Text = _settingsOpen ? "Close" : "Settings";
        }
    }

    private void ApplySettings()
    {
        Visible = CombatStatsSettings.Instance.HudVisible;
        Refresh();
    }

    private void Refresh()
    {
        if (_statsLabel == null)
        {
            return;
        }

        RunStats stats = RunStatsTracker.Instance.Stats;
        List<string> lines = [$"Combats: {stats.CombatsEntered}"];

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Damage))
        {
            lines.Add($"Damage dealt: {stats.DamageDealt}   Kills: {stats.EnemiesKilled}");
            lines.Add($"Enemy block removed: {stats.EnemyBlockRemoved}   Overkill: {stats.OverkillDamage}");
        }

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Defense))
        {
            lines.Add($"Block gained: {stats.BlockGained}   Damage prevented: {stats.BlockPrevented}");
            lines.Add($"Damage taken: {stats.DamageTaken}   Healing: {stats.HealingReceived}");
        }

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Cards))
        {
            lines.Add($"Cards - played {stats.CardsPlayed}, generated {stats.CardsGenerated}, drawn {stats.CardsDrawn}");
            lines.Add($"Cards - discarded {stats.CardsDiscarded}, exhausted {stats.CardsExhausted}");
        }

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Resources))
        {
            lines.Add($"Energy spent: {stats.EnergySpent}   Potions: {stats.PotionsUsed}");
            lines.Add($"Orbs channeled: {stats.OrbsChanneled}   Summons: {stats.SummonsCreated}");
            lines.Add($"Stars: +{stats.StarsGained} / -{stats.StarsSpent}");
        }

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Powers))
        {
            lines.Add($"Powers applied: {stats.PowerApplications} ({stats.PowerStacksApplied:0.#} stacks)");
        }

        if (CombatStatsSettings.Instance.IsSectionVisible(StatsSection.Debuffs))
        {
            string debuffs = stats.DebuffStacksById.Count == 0
                ? "none"
                : string.Join(", ", stats.DebuffStacksById
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key)
                    .Take(6)
                    .Select(entry => $"{entry.Key} {entry.Value:0.#}"));
            lines.Add($"Debuffs applied: {stats.DebuffApplications} ({stats.DebuffStacksApplied:0.#} stacks)");
            lines.Add($"Debuffs: {debuffs}");
        }

        _statsLabel.Text = string.Join("\n", lines);
    }
}
