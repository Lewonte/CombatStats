using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CombatStats.CombatStatsCode;

/// <summary>
/// Holds statistics for every player visible to this client. It only observes game events and never changes game state.
/// </summary>
public sealed class RunStatsTracker
{
    public static RunStatsTracker Instance { get; } = new();

    private readonly HashSet<ulong> _playersWithIgnoredStartingHp = [];
    private RunStatsRecord? _activeRun;

    public RunStats Stats { get; private set; } = new();
    public IReadOnlyList<PlayerRunStats> PlayerStats => _activeRun?.PlayerStats ?? [];
    public RunStatsRecord? ActiveRun => _activeRun;

    public event Action? Changed;

    public void OpenRun(RunState state)
    {
        _playersWithIgnoredStartingHp.Clear();
        _activeRun = RunStatsStore.OpenRun(state);
        EnsurePlayers(state);
        Stats = GetStats(LocalContext.GetMe(state));
        Changed?.Invoke();
    }

    public void MarkCurrentRunCompleted()
    {
        if (_activeRun == null || _activeRun.Completed)
        {
            return;
        }

        _activeRun.Completed = true;
        _activeRun.CompletedAt = DateTimeOffset.UtcNow;
        Persist();
        Changed?.Invoke();
    }

    public void StartCombat()
    {
        foreach (PlayerRunStats playerStats in PlayerStats)
        {
            playerStats.Stats.CombatsEntered++;
        }
        NotifyChanged();
    }

    public void RecordDamage(Creature receiver, Creature? dealer, DamageResult result)
    {
        Player? dealerPlayer = GetOwner(dealer);
        if (dealerPlayer != null && receiver.IsEnemy)
        {
            RunStats stats = GetStats(dealerPlayer);
            stats.DamageDealt += result.UnblockedDamage;
            stats.EnemyBlockRemoved += result.BlockedDamage;
            stats.OverkillDamage += result.OverkillDamage;

            if (result.WasTargetKilled)
            {
                stats.EnemiesKilled++;
            }
        }

        Player? receiverPlayer = GetOwner(receiver);
        if (receiverPlayer != null)
        {
            RunStats stats = GetStats(receiverPlayer);
            stats.DamageTaken += result.UnblockedDamage;
            stats.BlockPrevented += result.BlockedDamage;
        }

        NotifyChanged();
    }

    public void RecordBlock(Creature receiver, int amount)
    {
        Player? player = GetOwner(receiver);
        if (player != null)
        {
            GetStats(player).BlockGained += amount;
            NotifyChanged();
        }
    }

    public void RecordCardPlayed(CardModel card)
    {
        Player? player = GetOwner(card);
        if (player != null)
        {
            GetStats(player).CardsPlayed++;
            NotifyChanged();
        }
    }

    public void RecordCardGenerated(CardModel card, Player? creator)
    {
        Player? player = creator ?? GetOwner(card);
        if (player != null)
        {
            GetStats(player).CardsGenerated++;
            NotifyChanged();
        }
    }

    public void RecordCardDrawn(CardModel card)
    {
        Player? player = GetOwner(card);
        if (player != null)
        {
            GetStats(player).CardsDrawn++;
            NotifyChanged();
        }
    }

    public void RecordCardDiscarded(CardModel card)
    {
        Player? player = GetOwner(card);
        if (player != null)
        {
            GetStats(player).CardsDiscarded++;
            NotifyChanged();
        }
    }

    public void RecordCardExhausted(CardModel card)
    {
        Player? player = GetOwner(card);
        if (player != null)
        {
            GetStats(player).CardsExhausted++;
            NotifyChanged();
        }
    }

    public void RecordEnergySpent(Player player, int amount)
    {
        GetStats(player).EnergySpent += amount;
        NotifyChanged();
    }

    public void RecordPotionUsed(PotionModel potion)
    {
        Player? player = GetOwner(potion);
        if (player != null)
        {
            GetStats(player).PotionsUsed++;
            NotifyChanged();
        }
    }

    public void RecordOrbChanneled(OrbModel orb)
    {
        GetStats(orb.Owner).OrbsChanneled++;
        NotifyChanged();
    }

    public void RecordSummoned(Player player, int amount)
    {
        GetStats(player).SummonsCreated += amount;
        NotifyChanged();
    }

    public void RecordStars(Player player, int amount)
    {
        RunStats stats = GetStats(player);

        if (amount >= 0)
        {
            stats.StarsGained += amount;
        }
        else
        {
            stats.StarsSpent -= amount;
        }

        NotifyChanged();
    }

    public void RecordPowerApplied(PowerModel power, decimal amount, Creature? applier)
    {
        Player? player = GetOwner(applier);
        if (player == null || !power.Owner.IsEnemy || amount <= 0)
        {
            return;
        }

        RunStats stats = GetStats(player);
        stats.PowerApplications++;
        stats.PowerStacksApplied += amount;

        if (power.GetTypeForAmount(amount) == PowerType.Debuff)
        {
            stats.DebuffApplications++;
            stats.DebuffStacksApplied += amount;
            stats.AddDebuff(power.Id.Entry, amount);
        }

        NotifyChanged();
    }

    public void RecordHealing(Creature creature, int hpBeforeHealing)
    {
        Player? player = GetOwner(creature);
        if (player == null)
        {
            return;
        }

        if (!_playersWithIgnoredStartingHp.Contains(player.NetId) &&
            !CombatManager.Instance.IsInProgress &&
            hpBeforeHealing == 0 &&
            creature.CurrentHp == creature.MaxHp)
        {
            _playersWithIgnoredStartingHp.Add(player.NetId);
            return;
        }

        GetStats(player).HealingReceived += Math.Max(0, creature.CurrentHp - hpBeforeHealing);
        NotifyChanged();
    }

    private void EnsurePlayers(RunState state)
    {
        foreach (Player player in state.Players)
        {
            EnsurePlayer(player, LocalContext.IsMe(player) ? "You" : $"Player {PlayerStats.Count + 1}");
        }

        if (PlayerStats.Count == 0)
        {
            return;
        }

        // Records made by earlier versions only contain local stats. Preserve those when a run is resumed.
        PlayerRunStats local = PlayerStats.FirstOrDefault(entry => entry.PlayerId == _activeRun!.LocalPlayerId) ?? PlayerStats[0];
        if (local.Stats.IsEmpty() && !_activeRun!.Stats.IsEmpty())
        {
            local.Stats = _activeRun.Stats;
        }
    }

    private RunStats GetStats(Player? player)
    {
        if (player == null)
        {
            return Stats;
        }

        return EnsurePlayer(player, LocalContext.IsMe(player) ? "You" : $"Player {PlayerStats.Count + 1}").Stats;
    }

    private PlayerRunStats EnsurePlayer(Player player, string fallbackName)
    {
        if (_activeRun == null)
        {
            return new PlayerRunStats { PlayerId = player.NetId, DisplayName = fallbackName };
        }

        PlayerRunStats? existing = _activeRun.PlayerStats.FirstOrDefault(entry => entry.PlayerId == player.NetId);
        if (existing != null)
        {
            if (LocalContext.IsMe(player))
            {
                existing.DisplayName = "You";
            }
            return existing;
        }

        PlayerRunStats entry = new() { PlayerId = player.NetId, DisplayName = fallbackName };
        _activeRun.PlayerStats.Add(entry);
        return entry;
    }

    private static Player? GetOwner(Creature? creature) => creature?.Player ?? creature?.PetOwner;

    private static Player? GetOwner(object model)
    {
        return model.GetType().GetProperty("Owner")?.GetValue(model) as Player
            ?? model.GetType().GetProperty("Player")?.GetValue(model) as Player;
    }

    private void NotifyChanged()
    {
        Persist();
        Changed?.Invoke();
    }

    private void Persist()
    {
        if (_activeRun == null)
        {
            return;
        }

        PlayerRunStats? local = _activeRun.PlayerStats.FirstOrDefault(entry => entry.PlayerId == _activeRun.LocalPlayerId);
        _activeRun.Stats = local?.Stats ?? Stats;
        _activeRun.UpdatedAt = DateTimeOffset.UtcNow;
        RunStatsStore.Save(_activeRun);
    }
}

public sealed class RunStats
{
    public int CombatsEntered { get; set; }
    public int DamageDealt { get; set; }
    public int EnemyBlockRemoved { get; set; }
    public int OverkillDamage { get; set; }
    public int EnemiesKilled { get; set; }
    public int DamageTaken { get; set; }
    public int BlockPrevented { get; set; }
    public int BlockGained { get; set; }
    public int CardsPlayed { get; set; }
    public int CardsGenerated { get; set; }
    public int CardsDrawn { get; set; }
    public int CardsDiscarded { get; set; }
    public int CardsExhausted { get; set; }
    public int EnergySpent { get; set; }
    public int PotionsUsed { get; set; }
    public int OrbsChanneled { get; set; }
    public int SummonsCreated { get; set; }
    public int StarsGained { get; set; }
    public int StarsSpent { get; set; }
    public int PowerApplications { get; set; }
    public decimal PowerStacksApplied { get; set; }
    public int DebuffApplications { get; set; }
    public decimal DebuffStacksApplied { get; set; }
    public int HealingReceived { get; set; }
    public Dictionary<string, decimal> DebuffStacksById { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddDebuff(string id, decimal amount)
    {
        DebuffStacksById[id] = DebuffStacksById.GetValueOrDefault(id) + amount;
    }

    public bool IsEmpty() => DamageDealt == 0 && DamageTaken == 0 && BlockGained == 0 && CardsPlayed == 0 &&
        HealingReceived == 0 && PowerApplications == 0 && CombatsEntered == 0;
}

public sealed class PlayerRunStats
{
    public ulong PlayerId { get; set; }
    public string DisplayName { get; set; } = "Player";
    public RunStats Stats { get; set; } = new();
}
