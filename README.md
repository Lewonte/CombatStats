# CombatStats

An informational Slay the Spire 2 mod that displays live, per-run combat statistics in the upper-right of the combat UI.

## Tracked statistics

- Damage dealt, enemy block removed, overkill, and kills
- Block gained, damage prevented by block, damage taken, and healing received
- Cards played, generated, drawn, discarded, and exhausted
- Energy spent, potions used, stars gained/spent, orbs channeled, and summons created
- Power applications and stacks applied to enemies
- Debuff applications and stacks, broken down by debuff ID (for example `Vulnerable`, `Weak`, and `Frail`)

Statistics are saved continuously for the active run and restored after save-and-quit. At the end of a run, CombatStats adds a full recap to the game-over screen; its **History** button browses the last 50 completed run summaries. The vanilla run-history screen also shows a compact CombatStats recap when a matching saved run is selected.

In multiplayer, each player records and saves only their own actions locally. This keeps the mod informational and avoids modifying multiplayer game state. The mod declares `affects_gameplay: false`.

## Display settings

Use the **Settings** button on the combat HUD to choose which statistic sections are shown. Press **F8** at any time during combat to hide or show the panel. These preferences are stored locally in `%AppData%\\SlayTheSpire2\\CombatStats\\settings.json` and survive game restarts.

## Build

Install MegaDot 4.5.1 and the .NET 9 SDK, then build `CombatStats.csproj`. The template copies the DLL, manifest, and PDB to the game's local `mods/CombatStats` directory automatically. The mod has no BaseLib dependency.

For a currently loaded run, statistics begin from the point this mod is loaded; past combat events are not reconstructed.
