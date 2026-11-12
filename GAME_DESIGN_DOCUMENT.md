# OUTRUN: AFTERMATH — Game Design Document

**Version:** 0.1.0  
**Status:** Phase 0 approved for implementation  
**Setting:** 2053, six years after The Great Collapse

## 1. Fantasy

Two teams fight over scarce resources in ruined territories. Rogues complete a mission and get out. Sentinels stop them. Matches are short, readable, and decided by teamwork: ground tactics or a crewed vehicle.

## 2. Pillars

1. **Asymmetric but fair.** Different objectives, same rules of physics, damage, and hitboxes.
2. **Roles matter.** Operators and vehicle seats are jobs, not skins.
3. **Every match is a mission.** A director picks a legal objective for the map, mode, and player count.
4. **Server tells the truth.** Clients predict; the server owns health, hits, objectives, rewards.
5. **No pay-to-win.** Cosmetics and convenience only.

## 3. Camera and fairness

Players may toggle first-person and third-person. Hitboxes are identical. Third-person may not reveal geometry the first-person camera could not legally see through walls; camera collision and a shared visibility query on the server prevent peek exploits. Aim assist on mobile is capped and logged; it does not alter damage.

## 4. Factions

### Rogues

Independent survivors: ex-soldiers, smugglers, engineers, mercenaries. They move resources, extract people and tech, and leave. They are not uniformly heroic; several operators will sell to whoever pays.

### Sentinels

A military-security force that holds settlements and roads. They intercept unauthorized transports and recover stolen goods. They are not uniformly just; several operators enforce order through fear and black sites.

## 5. Modes

### Ground Operations

1v1 to 4v4 on foot. Movement, shooting, stealth, revive, capture, elimination, mission completion.

### Convoy Operations

2–4 per team. One vehicle per team by default (expandable). Seats: Driver, Attacker, Support, Heavy/Recon. Manual seat swap. Auto-takeover if the driver is downed or eliminated. One occupant per seat. If the crew is wiped, the vehicle goes inactive.

## 6. Match types (both modes)

| Type | Rogues | Sentinels | Win |
|------|--------|-----------|-----|
| Standard Pursuit | Complete mission + reach destination | Intercept | Mission success vs prevention |
| Survival | Survive the timer | Capture or eliminate Rogues | Timer vs wipe/capture |
| Extraction | Collect, carry, extract | Stop extract, steal assets | Extract vs deny |
| Bounty Hunt | Protect or complete target objective | Capture the marked target | Target state |
| Team Elimination | Eliminate Sentinels | Eliminate Rogues | Last team standing (revives allowed by rules) |

Illegal combinations (for example a forest map with no drivable loop for Convoy) are excluded from the random pool.

## 7. Operators (launch 10)

All operators ship usable. Unlocks are cosmetics and alternate kits, not a power wall.

### Rogues

| Codename | Role | Passive | Primary | Ultimate |
|----------|------|---------|---------|----------|
| REX | Assault | Bonus hip-fire recovery after sprint | Frag burst (short stun) | Overrun: move speed + armor 6s |
| NOVA | Scout | Footstep radius reduced | Marker drone 12s | Recon pulse: reveal enemies 4s |
| MEDIC | Support | Faster ally revive | Stim dart (heal over time) | Field revive: remote 50% revive on one downed ally |
| TORQUE | Engineer | Repair pads faster | Deploy cover plate | EMP grenade: vehicles/gadgets 4s |
| SHADE | Ghost | Quieter vault/crouch | Smoke veil | Phantom step: 1.2s untargetable dash (still collides) |

### Sentinels

| Codename | Role | Passive | Primary | Ultimate |
|----------|------|---------|---------|----------|
| TITAN | Assault | Reduced explosive damage | Suppression cone | Bastion: deployable shield wall 8s |
| TRACE | Tracker | See recent footprints | Tracker dart | Hunt: marked target wall-silhouettes 6s |
| VIPER | Combat | Headshot flinch resist | Toxin round (DoT) | Killbox: proximity slow field 7s |
| CIRCUIT | Tac engineer | Hack interactive objects faster | Sensor tripwire | Gridlock: disable enemy gadgets 6s |
| FALCON | Interceptor | Faster vehicle enter/seat swap | Grapple line | Strike: high-angle smoke + ping |

Every ability has cooldown, duration, range, and a counter (smoke vs scan, EMP vs shield, interrupt on revive, etc.). Server rejects use if cooldown, resource, or line-of-sight rules fail.

## 8. Combat

Weapons are data-driven (`WeaponDefinition`). Categories: AR, SMG, shotgun, sniper, marksman, LMG, pistol, melee, vehicle-mounted, tactical. Identity comes from fire rate, recoil pattern, ADS time, damage falloff, pellet count, and movement multipliers — not from mesh swaps.

Lifecycle: Alive → Downed (if rules allow) → BeingRevived → Revived (partial HP + recovery window) or Eliminated. Solo matches have no teammate revive unless a match rule enables self-revive.

## 9. Vehicles (launch 6)

Raptor (pickup), Iron Fang (muscle), Bulwark (armored SUV), Titan Hauler (truck), Dust Runner (buggy), Phantom Bike (recon). Component damage: tires, engine, body, windows, mounts. Fire, disable, explode, abandon, repair. Faction vehicle abilities are loadout-gated, cooldown-gated, and counterable (EMP vs shield, scanner vs smoke).

## 10. Maps (launch 7)

Dead City, Dust Valley, Blackwood Forest, Frostbite Ridge, Ironworks, Hollow Creek, Ashfall. Each has roads, off-road, cover, interiors, extraction, spawns, and a convoy loop or an explicit “Ground only” flag.

## 11. Economy

Survivor Rank from XP. Unlocks: cosmetics, emotes, banners, titles, alternate visuals. **Banned from paid/unlock gates:** damage, HP, speed, armor, exclusive competitive abilities, superior weapon stats.

## 12. Out of scope for Phase 0–2

Final art, full voice, seven authored maps, live voice chat vendor, store payments, anti-cheat kernel driver.
