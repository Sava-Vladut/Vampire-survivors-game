# Power-Up Selection System

## What changed

The existing scene data remains compatible. `PowerUpChooser`, `PowerUpSelectionUI`,
`RandomUpgradeGenerator`, `WeaponUpgrades`, their serialized field names, the
`WeaponUpgrades.UpgradeType` numeric order, and the `AddMaxWeapons` /
`AddMaxAccessories` UnityEvent methods were retained.

The runtime flow is now:

1. `PowerUpSelectionUI` opens a selection session and asks the generator for
   transient offers.
2. `RandomUpgradeGenerator` runs each `IPowerUpOfferSource`.
3. The UI filters selectable offers and uses `PowerUpWeightedSelector` to sample
   offer references (not mutable list indices).
4. `PowerUpChooser` activates the selected object and explicitly invokes any
   `IPowerUpSelectionEffect` on its root.
5. The generator destroys unselected transient offers when the session closes.

Weapon upgrade behavior is assembled by `WeaponUpgradeCatalog` from:

- `KnifeUpgradeDefinitions`
- `ShooterUpgradeDefinitions`
- `WeaponTickUpgradeDefinitions`

`WeaponUpgrades` is now only the serialized runtime entry/compatibility component.

## Required Unity setup

The compatibility path auto-adds a `RandomUpgradeGenerator`, so existing scenes
continue to run after scripts compile. Do the following once in every gameplay
scene to make the setup explicit and editable:

1. Let Unity finish importing and confirm there are no red Console errors.
2. Select **Game Manager**.
3. Confirm it has `PowerUpChooser` and `PowerUpSelectionUI`.
4. Add `RandomUpgradeGenerator` if it is not already saved on the object.
5. In `RandomUpgradeGenerator`, assign the same `PowerUpChooser` (or leave it
   empty; it resolves the chooser on the same object) and tune offers per weapon
   and accessory.
6. In `PowerUpSelectionUI`, keep the existing panel, text, button, icon, audio,
   and volume references. Assign the saved generator to **Upgrade Generator**.
7. Set **Choices Per Selection**, first-selection behavior, rerolls, and first
   on-hit base chance.
8. Open **Tools > Power Ups > Upgrade Manager**, select **Upgrade Ranges**, and
   confirm rarity frequencies/multipliers and roll ranges.

No generated `WeaponUpgrades` or `AccessoriesUpgrades` components need to be
placed by hand; offer sources create them at runtime.

## Smoke test checklist

1. Enter Play Mode and trigger a level-up selection.
2. Verify three unique cards appear and the game pauses.
3. Reroll once. The offers should change while the game remains paused.
4. Pick a weapon or accessory, then trigger another selection.
5. Pick a generated upgrade and verify the target stat changes exactly once.
6. Confirm the target tooltip/stat card increments its upgrade count.
7. Test Skip and confirm the previous time scale and volume weight are restored.
8. Fill weapon/accessory slots and verify non-upgrade offers of that type are
   filtered while upgrades remain selectable.
9. Pick a Curse offer and verify the existing Twitch penalty still fires.

## Adding reusable base power-ups

Legacy in-scene entries can remain in `PowerUpChooser.powerUps`. For new
prefab-backed content:

1. Create **Assets > Create > Power Ups > Power-Up Definition**.
2. Fill presentation, tags, rarity, weight, and a prefab activation object.
3. Create **Assets > Create > Power Ups > Power-Up Catalog**.
4. Add definitions to the catalog and assign it to `PowerUpChooser` as
   **Initial Catalog**.

Use a prefab in an asset definition. Unity assets cannot safely retain references
to scene-only objects; keep those as legacy scene entries.

## Adding a weapon upgrade

1. Append a new value to `WeaponUpgrades.UpgradeType`. Never reorder existing
   values because the settings asset serializes their numeric values.
2. Add one definition in the matching target module's `Register` method.
3. Provide its title/description, value format, default range, eligibility rule,
   and mutation action.
4. Reopen **Tools > Power Ups > Upgrade Manager**. `EnsureAllRanges` adds the new
   configurable range to `Resources/GeneratedUpgradeSettings.asset`.
5. Test the common, uncommon, rare, and curse multipliers.

For an entirely new weapon family, add a new `WeaponUpgradeTarget`, a new
definition provider registered in `WeaponUpgradeCatalog`, and target discovery in
`WeaponUpgradeOfferSource`. The selection UI and chooser do not change.

## Adding a new offer family

Implement `IPowerUpOfferSource`. Either place that MonoBehaviour on the same
GameObject as `RandomUpgradeGenerator` (it is discovered automatically) or call
`RegisterSource` at runtime. Use the supplied generation context to create and
register transient offer objects so cleanup remains centralized.

## Adding a custom selection effect

Implement `IPowerUpSelectionEffect` on the root of an offer's activation object.
Return `false` when the effect cannot be applied; the chooser will roll back the
activation and leave the offer unselected. Gameplay mutations should happen in
`TryApply`, not `Awake` or `OnEnable`.

## Boundary with weapon rarity

`WeaponRarityController` and its reroll UI remain a separate item-generation
system. They still control a weapon/accessory's inherent rarity modifiers. This
system controls level-up offers and generated upgrades. Keeping those concerns
separate prevents a level-up reroll from rebuilding inherent item stats.
