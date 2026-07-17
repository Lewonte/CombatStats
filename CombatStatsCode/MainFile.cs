using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace CombatStats.CombatStatsCode;

//You're recommended but not required to keep all your code in this package and all your assets in the CombatStats folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "CombatStats"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        CombatStatsSettings.Instance.Load();
        RunManager.Instance.RunStarted += RunStatsTracker.Instance.OpenRun;

        Harmony harmony = new(ModId);
        harmony.PatchAll();

        Logger.Info("CombatStats initialized.");
    }
}
