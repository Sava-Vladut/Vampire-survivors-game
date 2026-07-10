# Weapons and Accessories

Current-checkout reference for the player weapons and prefab-backed accessories. Values come from `Assets/Prefabs/Player.prefab`, the accessory definitions/prefabs under `Assets/PowerUps/Accessories` and `Assets/Prefabs/Accessories`, and the Grasslands/Castle power-up lists.

## Shared rules

- The player can hold up to **2 weapons** and **2 accessories** in both gameplay scenes.
- All current weapons deal **Physical** damage.
- Weapon damage below is the serialized range shown by the weapon UI. Both `Knife`-type and `SimpleShooter`-type weapons apply a final **0.5x output multiplier** after the damage roll, critical hit, and accessory damage multipliers.
- A burst waits for the listed interval, then performs all burst hits with the listed spacing.
- `Max targets: unlimited` means every valid, unique enemy found in the attack area can be hit on that tick.
- Weapons and accessories can each receive up to **20 generated upgrades**.
- Generated upgrade rarity multipliers are **Common 1x**, **Uncommon 1.5x**, **Rare 2x**, and **Curse 3x**. Their configured relative frequencies are **1**, **0.5**, **0.2**, and **0.05** respectively.

## Weapons

### Knife

- **Availability:** Active by default. It is serialized in both scene offer pools at weight **100**, but startup synchronization treats the already-active Knife as selected and removes that offer from the available pool.
- **Attack:** Hits the closest enemy within **2.0** units every **0.5s**.
- **Damage:** **7-23**.
- **Targets:** **1** per tick.
- **Critical:** **50%** chance, **1.5x** damage.
- **Other base effects:** No splash, status, knockback, lifesteal, cull, or echo strike.
- **What it does:** A fast single-target melee strike aimed at the nearest enemy.

### Axe

- **Availability:** Both scene offer pools, weight **30**.
- **Attack:** Hits the closest enemy within **2.5** units every **1.7s**.
- **Damage:** **17-53**.
- **Targets:** **1** main target per tick.
- **Splash:** **1.0** radius around the main target for **100%** of the main-hit damage.
- **Critical:** **2%** chance, **2x** damage.
- **Status:** **85%** chance to apply **Bleeding** for **8s**.
- **Knockback:** **7**.
- **What it does:** A slow, heavy strike with strong knockback, full-damage area splash, and a high chance to bleed its target.

### Sword

- **Availability:** Both scene offer pools, weight **30**.
- **Attack:** A **2-hit burst** every **1.2s**, with **0.3s** between the two hits. Each hit targets the closest enemy within **5.25** units.
- **Damage:** **10-30** per hit.
- **Targets:** **1** main target per hit.
- **Splash:** **0.5** radius for **50%** of the main-hit damage.
- **Critical:** **20%** chance, **2x** damage.
- **Other base effects:** No status, knockback, lifesteal, cull, or echo strike.
- **What it does:** A long-reaching, balanced melee weapon that swings twice and adds a small half-damage splash.

### Demonic Aura

- **Prefab object name:** `Demon Aura`.
- **Availability:** Both scene offer pools as `Demonic aura`, weight **25**.
- **Attack:** Damages enemies within **1.9** units every **0.35s**.
- **Damage:** **5-15**.
- **Targets:** Unlimited.
- **Critical:** No base critical chance; multiplier remains **2x** if critical chance is later added.
- **Other base effects:** No splash, status, knockback, lifesteal, cull, or echo strike.
- **What it does:** A rapid point-blank aura that repeatedly damages every nearby enemy.

### Eye of Devastation

- **Prefab object name:** `Phantom Demonis`.
- **Availability:** Both scene offer pools as the misspelled `Eye of devastion`, weight **50**.
- **Attack:** Three eyes orbit at **1.5** units and **100 degrees/second**. Every **0.3s**, each eye checks a **0.7**-unit area.
- **Damage:** **2-8**.
- **Targets:** Unlimited unique targets across the three eye origins each tick.
- **Critical:** **2%** chance, **2x** damage.
- **Other base effects:** No splash, status, knockback, lifesteal, cull, or echo strike.
- **What it does:** Three fast-orbiting damage zones continuously grind down enemies that get close to an eye.

### Lightning Strikes

- **Availability:** Configured in `Player.prefab`, but **not present in the Grasslands or Castle offer pool**.
- **Attack:** A **5-hit burst** every **1.0s**, with **0.15s** between hits. Its origin moves to a random point within **4** units as the burst runs.
- **Damage:** **8-26** per strike.
- **Range:** **0.7** around the strike origin.
- **Targets:** **1** main target per strike.
- **Splash:** **1.0** radius for **50%** of the main-hit damage.
- **Critical:** **50%** chance, **1.5x** damage.
- **Other base effects:** No status, knockback, lifesteal, cull, or echo strike.
- **What it does:** A mobile multi-strike area attack, currently unreachable through normal scene offers.

### Blowgun

- **Availability:** Both scene offer pools, weight **25**.
- **Targeting:** A target transform follows the cursor at speed **20**.
- **Attack:** Fires every **0.7s**.
- **Damage:** **5-15** per projectile.
- **Projectiles:** **5** per attack in a **70-degree** random spread.
- **Projectile speed/lifetime:** **20** units/second for **1s**.
- **Penetration:** **1**.
- **Critical:** No base critical chance; multiplier is **2x** if critical chance is later added.
- **Other base effects:** No status, knockback, cull, chain, or fork shot.
- **What it does:** A short-lived, cursor-aimed shotgun blast with five widely spread projectiles.

### Burstfire Bow

- **Availability:** Both scene offer pools, weight **25**.
- **Targeting:** Tracks the nearest enemy within **10** units; target tracking refreshes every **0.5s**.
- **Attack:** A **3-shot burst** every **0.7s**, with **0.1s** between shots.
- **Damage:** **8-26** per projectile.
- **Projectiles:** **1** per burst shot with **10 degrees** of spread.
- **Projectile speed/lifetime:** **15** units/second for **3s**.
- **Penetration:** **1**.
- **Knockback:** **1**.
- **Critical:** **30%** chance, **2x** damage.
- **Other base effects:** No status, cull, chain, or fork shot.
- **What it does:** A nearest-enemy bow that delivers three quick, accurate shots with modest knockback.

### Longshot Crossbow

- **Availability:** Both scene offer pools, weight **40**.
- **Targeting:** Tracks the nearest enemy within **10** units; target tracking refreshes every **1s**.
- **Attack:** Fires every **1.0s**.
- **Damage:** **15-45** per projectile.
- **Projectiles:** **1** with **5 degrees** of spread.
- **Projectile speed/lifetime:** **20** units/second for **5s**.
- **Penetration:** **3**.
- **Critical:** **25%** chance, **3x** damage.
- **Other base effects:** No status, knockback, cull, chain, or fork shot.
- **What it does:** A slow-firing precision weapon with long-lived projectiles, high critical damage, and strong piercing.

### Phantom Aegis

- **Offer name:** `Phantom Aegis (invisible mod)`.
- **Availability:** Both scene offer pools, weight **40**.
- **Movement/targeting:** The weapon orbits at **2** units and **30 degrees/second**. Its target tracks the nearest enemy within **13** units and refreshes every **1s**.
- **Attack:** Fires every **0.6s** from **2 shield origins**.
- **Damage:** **5-17** per projectile.
- **Projectiles:** **1 per origin** (**2 total per attack**) with **40 degrees** of spread.
- **Projectile speed/lifetime:** **12** units/second for **3s**.
- **Penetration:** **1**.
- **Critical:** **5%** chance, **2x** damage.
- **Other base effects:** No status, knockback, cull, chain, or fork shot.
- **What it does:** An orbiting pair of shields that automatically fires two projectiles toward a nearby target.

## Accessories

All 12 real accessories are prefab-backed. Every accessory is available in Grasslands. Castle contains all of them except **Coat**.

Damage multipliers from Bloodied Banner, Grave Pact, Reaper's Ledger, and Witching Hourglass stack **multiplicatively** with one another.

| Accessory | Base stats/effect | What it does | Offer weight | Scene availability |
|---|---|---|---:|---|
| **Arcane Splitter Bandolier** | **+1 Projectile Count** | Adds one projectile to every player `SimpleShooter`, including currently inactive ranged weapons. | 10 | Grasslands, Castle |
| **Armor** | **+20 Armor**; after taking positive mitigatable damage, **+100% Armor for 3s** | Gives a large permanent armor boost and temporarily doubles effective armor after a direct hit. | 70 | Grasslands, Castle |
| **Bloodied Banner** | **+10% damage per nearby enemy**, up to **+100%**; radius **5** | Counts living `EnemyChaser` enemies within 5 units and scales all player weapon damage with the crowd size. Refreshes every **0.15s**. | 20 | Grasslands, Castle |
| **Boots** | **+0.3 Move Speed**, **+5 Evasion** | Permanently increases movement speed and adds a small evasion bonus. | 30 | Grasslands, Castle |
| **Coat** | **+20 Evasion**; after taking positive mitigatable damage, **+100% Evasion for 3s** | Gives a large permanent evasion boost and temporarily doubles effective evasion after a direct hit. | 40 | Grasslands only |
| **Grave Pact** | **+2% damage per missing 10% max HP**, up to **+20%** | Uses completed 10% missing-health steps, so the bonus rises at 10%, 20%, and later missing-health thresholds. Refreshes every **0.1s**. | 20 | Grasslands, Castle |
| **Ice Ring** | **+20 Max Health**, **+30% Cold Resist**, **+30% Fire Resist** | A health and elemental-defense accessory focused on cold and fire. | 30 | Grasslands, Castle |
| **Life Ring** | **+50 Max Health**, **+2 HP/second regeneration** | The largest flat-health accessory and a constant source of passive healing. | 10 | Grasslands, Castle |
| **Lightning Ring** | **+20 Max Health**, **+30% Lightning Resist**, **+30% Poison Resist** | A health and elemental-defense accessory focused on lightning and poison. | 30 | Grasslands, Castle |
| **Reaper's Ledger** | **+0.1% damage per enemy kill**, up to **+150%** | Permanently builds damage during the run when an `EnemyChaser` dies. It takes **1,500 kills** to reach the cap. | 20 | Grasslands, Castle |
| **Sanguine Chalice** | Heals **5% max HP every 10 enemy kills** | Tracks `EnemyChaser` kills and heals a rounded 5% of maximum health, with a minimum heal of 1. | 20 | Grasslands, Castle |
| **Witching Hourglass** | **+1% damage per second without taking damage**, up to **+50%** | Builds damage for up to 50 untouched seconds; any positive damage taken resets the bonus to zero. | 20 | Grasslands, Castle |

### Accessory upgrade-profile differences

- Every accessory has a maximum of **20** generated upgrades.
- The default profile excludes flat Move Speed, Dash Distance, Thorns, and Projectile Count rolls.
- **Boots** uses a movement profile that allows flat Move Speed and Dash Distance, but excludes Thorns and Projectile Count.
- **Armor** uses a defense profile that allows Thorns, but excludes flat Move Speed, Dash Distance, and Projectile Count.
- All other accessories use the default profile. Arcane Splitter Bandolier's **+1 Projectile Count** is its fixed base effect, not a default random-upgrade roll.

## Nonfunctional legacy Castle entries

Castle still serializes two old entries named **Fire Ring** and **Poison Ring**. Both are marked as accessories, but both have a null `powerUpObject` and null `sourceDefinition`. `PowerUpChooser.CanSelect` rejects offers without a `powerUpObject`, so these entries cannot currently be selected and have no gameplay stats/effect. They are not counted among the 12 working accessories above.

## Primary source files

- `Assets/Prefabs/Player.prefab`
- `Assets/Scenes/Grasslands.unity`
- `Assets/Scenes/Castle.unity`
- `Assets/PowerUps/Accessories/`
- `Assets/Prefabs/Accessories/`
- `Assets/Scripts/Weapon Behaviours/Knife.cs`
- `Assets/Scripts/Weapon Behaviours/SimpleShooter.cs`
- `Assets/Scripts/Weapon Behaviours/WeaponTick.cs`
- `Assets/Scripts/Systems/Power Up/AccessoryStatEffects.cs`
- `Assets/Scripts/Systems/Power Up/PlayerDamageModifierRegistry.cs`
