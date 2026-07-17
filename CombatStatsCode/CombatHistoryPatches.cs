using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CombatStats.CombatStatsCode;

// CombatHistory is the game's own post-resolution event stream. Patching it means the tracker sees
// final values after modifiers, block, and overkill have all been resolved.
[HarmonyPatch]
public static class CombatHistoryPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.DamageReceived))]
    private static void DamageReceived(Creature receiver, Creature? dealer, DamageResult result)
        => RunStatsTracker.Instance.RecordDamage(receiver, dealer, result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.BlockGained))]
    private static void BlockGained(Creature receiver, int amount)
        => RunStatsTracker.Instance.RecordBlock(receiver, amount);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
    private static void CardPlayFinished(CardPlay cardPlay)
        => RunStatsTracker.Instance.RecordCardPlayed(cardPlay.Card);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardGenerated))]
    private static void CardGenerated(CardModel card, Player? creator)
        => RunStatsTracker.Instance.RecordCardGenerated(card, creator);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardDrawn))]
    private static void CardDrawn(CardModel card)
        => RunStatsTracker.Instance.RecordCardDrawn(card);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardDiscarded))]
    private static void CardDiscarded(CardModel card)
        => RunStatsTracker.Instance.RecordCardDiscarded(card);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardExhausted))]
    private static void CardExhausted(CardModel card)
        => RunStatsTracker.Instance.RecordCardExhausted(card);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.EnergySpent))]
    private static void EnergySpent(int amount, Player player)
        => RunStatsTracker.Instance.RecordEnergySpent(player, amount);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.PotionUsed))]
    private static void PotionUsed(PotionModel potion)
        => RunStatsTracker.Instance.RecordPotionUsed(potion);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.OrbChanneled))]
    private static void OrbChanneled(OrbModel orb)
        => RunStatsTracker.Instance.RecordOrbChanneled(orb);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.Summoned))]
    private static void Summoned(int amount, Player player)
        => RunStatsTracker.Instance.RecordSummoned(player, amount);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.StarsModified))]
    private static void StarsModified(int amount, Player player)
        => RunStatsTracker.Instance.RecordStars(player, amount);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.PowerReceived))]
    private static void PowerReceived(PowerModel power, decimal amount, Creature? applier)
        => RunStatsTracker.Instance.RecordPowerApplied(power, amount, applier);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.HealInternal))]
    private static void HealPrefix(Creature __instance, out int __state)
        => __state = __instance.CurrentHp;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.HealInternal))]
    private static void HealPostfix(Creature __instance, int __state)
        => RunStatsTracker.Instance.RecordHealing(__instance, __state);
}
