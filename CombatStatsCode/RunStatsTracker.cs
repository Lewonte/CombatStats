using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CombatStats.CombatStatsCode;

/// <summary>
/// Holds informational statistics for the currently active run. This class never writes to game state.
/// </summary>
public sealed class RunStatsTracker
{
    public static RunStatsTracker Instance { get; } = new();

    private readonly HashSet<ulong> _playersWithIgnoredStartingHp = [];

    public RunStats Stats { get; private set; } = new();

    public event Action? Changed;

    public void Reset()
    {
        Stats = new RunStats();
        _playersWithIgnoredStartingHp.Clear();
        NotifyChanged();
    }

    public void StartCombat()
    {
        Stats.CombatsEntered++;
        NotifyChanged();
    }

    public void RecordDamage(Creature receiver, Creature? dealer, DamageResult result)
    {
        if (IsMine(dealer) && receiver.IsEnemy)
        {
            Stats.DamageDealt += result.UnblockedDamage;
            Stats.EnemyBlockRemoved += result.BlockedDamage;
            Stats.OverkillDamage += result.OverkillDamage;

            if (result.WasTargetKilled)
            {
                Stats.EnemiesKilled++;
            }
        }

        if (LocalContext.IsMe(receiver))
        {
            Stats.DamageTaken += result.UnblockedDamage;
            Stats.BlockPrevented += result.BlockedDamage;
        }

        NotifyChanged();
    }

    public void RecordBlock(Creature receiver, int amount)
    {
        if (IsMine(receiver))
        {
            Stats.BlockGained += amount;
            NotifyChanged();
        }
    }

    public void RecordCardPlayed(CardModel card)
    {
        if (LocalContext.IsMine(card))
        {
            Stats.CardsPlayed++;
            NotifyChanged();
        }
    }

    public void RecordCardGenerated(CardModel card, Player? creator)
    {
        if (LocalContext.IsMe(creator) || LocalContext.IsMine(card))
        {
            Stats.CardsGenerated++;
            NotifyChanged();
        }
    }

    public void RecordCardDrawn(CardModel card)
    {
        if (LocalContext.IsMine(card))
        {
            Stats.CardsDrawn++;
            NotifyChanged();
        }
    }

    public void RecordCardDiscarded(CardModel card)
    {
        if (LocalContext.IsMine(card))
        {
            Stats.CardsDiscarded++;
            NotifyChanged();
        }
    }

    public void RecordCardExhausted(CardModel card)
    {
        if (LocalContext.IsMine(card))
        {
            Stats.CardsExhausted++;
            NotifyChanged();
        }
    }

    public void RecordEnergySpent(Player player, int amount)
    {
        if (LocalContext.IsMe(player))
        {
            Stats.EnergySpent += amount;
            NotifyChanged();
        }
    }

    public void RecordPotionUsed(PotionModel potion)
    {
        if (LocalContext.IsMine(potion))
        {
            Stats.PotionsUsed++;
            NotifyChanged();
        }
    }

    public void RecordOrbChanneled(OrbModel orb)
    {
        if (LocalContext.IsMe(orb.Owner))
        {
            Stats.OrbsChanneled++;
            NotifyChanged();
        }
    }

    public void RecordSummoned(Player player, int amount)
    {
        if (LocalContext.IsMe(player))
        {
            Stats.SummonsCreated += amount;
            NotifyChanged();
        }
    }

    public void RecordStars(Player player, int amount)
    {
        if (!LocalContext.IsMe(player))
        {
            return;
        }

        if (amount >= 0)
        {
            Stats.StarsGained += amount;
        }
        else
        {
            Stats.StarsSpent -= amount;
        }

        NotifyChanged();
    }

    public void RecordPowerApplied(PowerModel power, decimal amount, Creature? applier)
    {
        if (!IsMine(applier) || !power.Owner.IsEnemy || amount <= 0)
        {
            return;
        }

        Stats.PowerApplications++;
        Stats.PowerStacksApplied += amount;

        if (power.GetTypeForAmount(amount) == PowerType.Debuff)
        {
            Stats.DebuffApplications++;
            Stats.DebuffStacksApplied += amount;
            Stats.AddDebuff(power.Id.Entry, amount);
        }

        NotifyChanged();
    }

    public void RecordHealing(Creature creature, int hpBeforeHealing)
    {
        if (!LocalContext.IsMe(creature))
        {
            return;
        }

        // A newly created player is healed from 0 to their maximum HP during run setup.
        // Ignore that one baseline event, but retain healing from campfires, events, and combat.
        Player? player = creature.Player;
        if (player != null &&
            !_playersWithIgnoredStartingHp.Contains(player.NetId) &&
            !CombatManager.Instance.IsInProgress &&
            hpBeforeHealing == 0 &&
            creature.CurrentHp == creature.MaxHp)
        {
            _playersWithIgnoredStartingHp.Add(player.NetId);
            return;
        }

        Stats.HealingReceived += Math.Max(0, creature.CurrentHp - hpBeforeHealing);
        NotifyChanged();
    }

    private static bool IsMine(Creature? creature) =>
        LocalContext.IsMe(creature) || LocalContext.IsMe(creature?.PetOwner);

    private void NotifyChanged() => Changed?.Invoke();
}

public sealed class RunStats
{
    private readonly Dictionary<string, decimal> _debuffStacksById = new(StringComparer.OrdinalIgnoreCase);

    public int CombatsEntered { get; internal set; }
    public int DamageDealt { get; internal set; }
    public int EnemyBlockRemoved { get; internal set; }
    public int OverkillDamage { get; internal set; }
    public int EnemiesKilled { get; internal set; }
    public int DamageTaken { get; internal set; }
    public int BlockPrevented { get; internal set; }
    public int BlockGained { get; internal set; }
    public int CardsPlayed { get; internal set; }
    public int CardsGenerated { get; internal set; }
    public int CardsDrawn { get; internal set; }
    public int CardsDiscarded { get; internal set; }
    public int CardsExhausted { get; internal set; }
    public int EnergySpent { get; internal set; }
    public int PotionsUsed { get; internal set; }
    public int OrbsChanneled { get; internal set; }
    public int SummonsCreated { get; internal set; }
    public int StarsGained { get; internal set; }
    public int StarsSpent { get; internal set; }
    public int PowerApplications { get; internal set; }
    public decimal PowerStacksApplied { get; internal set; }
    public int DebuffApplications { get; internal set; }
    public decimal DebuffStacksApplied { get; internal set; }
    public int HealingReceived { get; internal set; }

    public IReadOnlyDictionary<string, decimal> DebuffStacksById => _debuffStacksById;

    internal void AddDebuff(string id, decimal amount)
    {
        _debuffStacksById[id] = _debuffStacksById.GetValueOrDefault(id) + amount;
    }
}
