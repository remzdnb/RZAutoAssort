RZAutoAssort was built to make trader assort customization easier — adding items to the game and being able to buy them immediately without manually editing assort files every time.

This mod approaches the problem from two angles, which can be used together or independently depending on what you need.

---

## ⚠️ Default Configuration Warning

Out of the box, the config ships with `EnableAutoRouting: true` and `ForceRouteAll: true` — this is intentional as a demo mode that routes every single item in the handbook to a trader so you can immediately see what the mod does. This is not meant for a real playthrough.

For a more playable setup:

- Set `ForceRouteAll` to `false` — only configured category routes will be used
- Set `EnableOverrides` to `true` — your custom per-item overrides will apply
- Set `EnableManualOffers` to `true` — your manually defined trades will be injected

From there you can tweak `CategoryRoutes`, `Overrides`, and `ManualOffers` to your liking.

---

## 🔀 Feature 1 — Automatic Handbook Routing

The first and most powerful feature is automatic routing: the mod reads every item present in the handbook at runtime and automatically assigns it to a trader based on a category map you define in the config.

The routing system follows the handbook's category hierarchy, so if you define a route for "Weapons", every sub-category (pistols, rifles, shotguns, etc.) inherits that route automatically. You can override specific sub-categories individually to send them somewhere else. The config ships with a full pre-built category map covering all vanilla item categories, so out of the box everything already goes somewhere sensible — Prapor gets ammo and magazines, Therapist gets meds, Peacekeeper gets guns, and so on.

### Why is this useful?

**For exploring modded content.** If you're running mods that add new items to the game and want to actually find and buy them without hunting through traders or the flea market, `RouteModdedItemsOnly` is for you. The mod detects which items weren't present in the vanilla handbook and routes only those to traders — every item added by every mod you have installed shows up at a trader immediately, at handbook price, ready to use.

**For visibility.** `ForceRouteAll` bypasses all filters and blacklists and routes literally every item in the handbook, including items that are normally hidden or uncategorized. Useful when you're trying to find an item that isn't appearing anywhere and you need to figure out why. Combined with `AllItemsExamined` (which marks every item as identified on your profile), nothing is hidden.

**For total economy overhauls.** The routing system is the backbone of any trader customization. Instead of hand-editing dozens of assort JSON files and keeping them in sync with every SPT update, you maintain a single config file that describes the intent — "pistols go to Peacekeeper at loyalty level 1 at handbook price" — and the mod handles the rest.

### Modded item detection

When `RouteModdedItemsOnly` or `RouteVanillaItemsOnly` is enabled, the mod performs a diff between the runtime handbook and a `vanilla_handbook.json` file placed in the mod's root folder. Any item present in the runtime handbook but absent from the vanilla one is considered modded. This file is a plain copy of the vanilla SPT `handbook.json` and needs to be updated when you update SPT.

### Overrides

Individual items can be overridden regardless of their category route. You can redirect a specific item to a different trader, set a fixed price in roubles, set a price multiplier on top of the handbook price, require a specific loyalty level, set a fixed stack count, or replace the payment entirely with barter items.

---

## 🛒 Feature 2 — Manual Offers

The second feature is completely independent from auto-routing and works whether auto-routing is enabled or not. Manual offers let you define specific trades for specific traders with full control over every parameter.

Each offer supports:

- **Rouble price** or **barter payment** (or both combined — the payment is additive)
- **Stack count** (`-1` for unlimited)
- **Loyalty level requirement**
- **Durability** for weapons and armor
- **Manual children** — explicitly define attachments or mods on a weapon
- **Auto-resolved children** — for items with required slots (armor plates, etc.), the mod automatically injects the correct child items by reading the item's template from the database, recursively. You don't have to worry about a armor vest being invalid because it's missing its plate carrier slot.

Manual offers are always injected regardless of `ForceRouteAll`, blacklists, or any other routing setting. They're processed first, before auto-routing runs.

This makes manual offers the right tool for anything that doesn't fit cleanly into category routing: a barter trade for a specific case, a weapon preset sold at a specific trader at a specific loyalty level, an item that belongs to multiple categories and needs special handling.

---

## ⚙️ Configuration Reference

The full config is in `config/userConfig.json`.

### Global flags

**`ClearDefaultAssorts`** *(default: true)*
Clears all vanilla trader assorts before injecting custom ones. Set to `false` to add offers on top of existing ones.

**`EnableAutoRouting`** *(default: true)*
Master switch for automatic handbook → trader routing.

**`ForceRouteAll`** *(default: false)*
Routes every handbook item ignoring blacklists and disabled routes. Only applies when `EnableAutoRouting` is true. Useful to find items not showing up anywhere.

**`RouteModdedItemsOnly`** *(default: false)*
Only routes items not present in `vanilla_handbook.json`. Mutually exclusive with `RouteVanillaItemsOnly`.

**`RouteVanillaItemsOnly`** *(default: false)*
Only routes items present in `vanilla_handbook.json`. Mutually exclusive with `RouteModdedItemsOnly`.

**`EnableOverrides`** *(default: true)*
Enables per-TPL overrides defined in `Overrides`.

**`EnableManualOffers`** *(default: true)*
Enables manual offers defined in `ManualOffers`.

**`AllItemsExamined`** *(default: false)*
Marks every item as examined on all profile templates.

**`UnlockAllTraders`** *(default: false)*
Sets all traders as unlocked by default on all profile templates.

**`FallbackTrader`** *(default: null)*
When `ForceRouteAll` is true, items with no matching category route are sent here.

### Blacklists

> ⚠️ Blacklists only affect auto-routing. They do not affect manual offers, overrides, or vanilla assorts.

**`UseStaticBlacklist`**
Applies the `StaticBlacklist` array — items that are broken, invisible, or non-functional in-game. These are also excluded from the encyclopedia even when `AllItemsExamined` is true.

**`UseUserBlacklist`**
Applies the `UserBlacklist` array — items you want to hide from traders for gameplay or balance reasons. Unlike the static blacklist, these are still added to the encyclopedia when `AllItemsExamined` is true.

### CategoryRoutes

Each entry maps a handbook category ID to a trader. Sub-categories inherit the parent route automatically. Set `Enabled: false` to disable a route without deleting it.

```json
{ "Enabled": true, "CategoryId": "5b5f792486f77447ed5636b3", "TraderName": "Peacekeeper", "PriceMultiplier": 1.0, "LoyaltyLevel": 1 }
```

### Overrides

Per-TPL overrides take precedence over category routes.

```json
{
  "ItemTpl": "5c093ca986f7740a1867ab12",
  "TraderName": "Jaeger",
  "PriceRoubles": 500000,
  "PriceMultiplier": 1.0,
  "LoyaltyLevel": 4,
  "StackCount": 1,
  "BarterItems": [
    { "ItemTpl": "5d235b4d86f7742e017bc88a", "Count": 3 }
  ]
}
```

### ManualOffers

Offers are grouped by trader ID. Each offer supports rouble price, barter items, stack count, loyalty level, durability, and explicit children.

```json
{
  "Id": "54cb50c76803fa8b248b4571",
  "Offers": [
    {
      "ItemTpl": "5a16b8a9fcdbcb00165aa6ca",
      "StackCount": -1,
      "LoyaltyLevel": 1,
      "Durability": 100,
      "PriceRoubles": 45000,
      "Children": [],
      "BarterItems": []
    }
  ]
}
```

---

## 🔧 Compatibility

Any mod that modifies trader assorts must run at `RagfairCallbacks - 2` or earlier to be compatible.

RZAutoAssort scans all traders after injection and removes traders with no assorts from the ragfair config to prevent error spam — this scan runs at `RagfairCallbacks - 1` and needs all assort data to be final by then.

### Tested compatible mods

Item-adding mods confirmed working with `RouteModdedItemsOnly` :

- [More Energy Drinks](https://forge.sp-tarkov.com/mod/1688/more-energy-drinks) by Hood
- [WTT - Content Backport](https://forge.sp-tarkov.com/mod/2512/wtt-content-backport) by GrooveypenguinX

---

## 📝 Notes

This is my first SPT mod and an early beta release. I've been learning the SPT C# server codebase for about a week, so things might not be perfect — feedback and bug reports are welcome. I'll be actively working on improving it over the coming weeks. Use at your own risk.

RZAutoAssort is also the first building block of a larger project: a full economy conversion mod for SPT. The routing and manual offer system is designed to be the foundation that everything else builds on top of. So stay tuned, more to come !

If you have questions or just want to talk about the mod, feel free to hit me up on Discord — **remzdnb**.
