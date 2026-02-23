// RemzDNB - 2026
// ReSharper disable InvertIf

using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;

namespace RZAutoAssort;

[Injectable(TypePriority = OnLoadOrder.RagfairCallbacks - 1)]
public class AssortsPatcher(ILogger<AssortsPatcher> logger, DatabaseService databaseService, ConfigLoader configLoader) : IOnLoad
{
    private Dictionary<MongoId, TemplateItem>? _itemTemplates;

    public Task OnLoad()
    {
        _itemTemplates = databaseService.GetTables().Templates?.Items;

        var traders = databaseService.GetTraders();
        var config = configLoader.Load<AutoAssortConfig>(AutoAssortConfig.FileName);
        var handbook = databaseService.GetTables().Templates?.Handbook;

        if (handbook is null || _itemTemplates is null)
        {
            logger.LogWarning("[RZAutoAssort] Handbook or item templates null — skipping.");
            return Task.CompletedTask;
        }

        // 1. Reset all assorts.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        if (config.ClearDefaultAssorts)
        {
            foreach (var (id, trader) in traders)
            {
                if (trader is null)
                {
                    continue;
                }

                trader.Assort = new TraderAssort
                {
                    Items = new List<Item>(),
                    BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                    LoyalLevelItems = new Dictionary<MongoId, int>(),
                    NextResupply = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
                };
            }
        }

        // 2. Manual offers (always injected, regardless of ForceGenerateAll).
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        if (config.EnableManualOffers)
        {
            var manualById = config.ManualOffers.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var (id, trader) in traders)
            {
                if (manualById.TryGetValue(id.ToString(), out var manualOffers))
                {
                    InjectManualOffers(trader.Assort, manualOffers.Offers);
                }
            }
        }

        // 3. Build blacklist. In ForceGenerateAll mode, blacklists are ignored.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!config.ForceRouteAll)
        {
            if (config.UseStaticBlacklist)
            {
                blacklist.UnionWith(config.StaticBlacklist); // was: StaticBlacklist.Tpls
            }

            if (config.UseUserBlacklist)
            {
                blacklist.UnionWith(config.UserBlacklist);
            }
        }

        // 4. Build modded item set (diff between runtime handbook and vanilla_handbook.json).
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        if (config.RouteModdedItemsOnly && config.RouteVanillaItemsOnly)
        {
            logger.LogWarning(
                "[RZAutoAssort] RouteModdedItemsOnly and ExcludeModdedItems are both true — "
                    + "these options are mutually exclusive. Both will be ignored and all items will be routed normally."
            );
            config.RouteModdedItemsOnly = false;
            config.RouteVanillaItemsOnly = false;
        }

        HashSet<string>? moddedTpls = null;
        if (config.RouteModdedItemsOnly || config.RouteVanillaItemsOnly)
        {
            moddedTpls = BuildModdedItemSet(handbook);
            logger.LogInformation("[RZAutoAssort] Detected {Count} modded item(s) via vanilla_handbook.json diff.", moddedTpls.Count);
        }

        // 5. Build category route map. In ForceGenerateAll mode, disabled routes are included anyway.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        var activeRoutes = config.ForceRouteAll ? config.CategoryRoutes : config.CategoryRoutes.Where(r => r.Enabled).ToList();

        var categoryToRoute = BuildCategoryRouteMap(handbook.Categories, activeRoutes);
        var overrides = config.Overrides.ToDictionary(o => o.ItemTpl, StringComparer.OrdinalIgnoreCase);

        int routed = 0,
            overridden = 0,
            skipped = 0;

        // 6. Automatic handbook → trader routing.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

        if (config.EnableAutoRouting)
        {
            foreach (var hbItem in handbook.Items)
            {
                var itemTpl = hbItem.Id.ToString();

                if (blacklist.Contains(itemTpl))
                {
                    skipped++;
                    continue;
                }

                // Modded item filter.

                if (moddedTpls is not null)
                {
                    var isModded = moddedTpls.Contains(itemTpl);
                    if (config.RouteModdedItemsOnly && !isModded || config.RouteVanillaItemsOnly && isModded)
                    {
                        skipped++;
                        continue;
                    }
                }

                // Override.

                if (config.EnableOverrides && overrides.TryGetValue(itemTpl, out var ov))
                {
                    if (string.IsNullOrEmpty(ov.TraderName))
                    {
                        skipped++;
                        continue;
                    }

                    var ovTraderId = TraderIds.FromName(ov.TraderName);
                    if (ovTraderId is null || !traders.TryGetValue(ovTraderId, out var ovTrader))
                    {
                        logger.LogWarning("[RZAutoAssort] Override trader '{T}' not found for '{Tpl}'.", ov.TraderName, itemTpl);
                        skipped++;
                        continue;
                    }

                    //var price = ov.PriceRoubles > 0 ? ov.PriceRoubles : (int)Math.Round((hbItem.Price ?? 0) * ov.PriceMultiplier);

                    InjectAutoOffer(ovTrader.Assort, hbItem.Id, ov.PriceRoubles, ov.StackCount, ov.LoyaltyLevel, ov.BarterItems, 100);
                    overridden++;
                    continue;
                }

                // Normal reroute.

                if (!categoryToRoute.TryGetValue(hbItem.ParentId, out var route))
                {
                    if (config.ForceRouteAll && config.FallbackTrader is not null)
                    {
                        var fallbackId = TraderIds.FromName(config.FallbackTrader);
                        if (fallbackId is not null && traders.TryGetValue(fallbackId, out var fallbackTrader))
                        {
                            var fallbackPrice = Math.Max(1, (int)Math.Round(hbItem.Price ?? 0));
                            InjectAutoOffer(fallbackTrader.Assort, hbItem.Id, fallbackPrice, -1, 1, new List<BarterItem>(), 100);
                            routed++;
                        }
                    }
                    else
                    {
                        skipped++;
                    }
                    continue;
                }

                var traderId = TraderIds.FromName(route.TraderName);
                if (traderId is null || !traders.TryGetValue(traderId, out var trader))
                {
                    logger.LogWarning("[RZAutoAssort] Route trader '{T}' not found.", route.TraderName);
                    skipped++;
                    continue;
                }

                var handbookPrice = Math.Max(1, (int)Math.Round((hbItem.Price ?? 0) * route.PriceMultiplier));
                InjectAutoOffer(trader.Assort, hbItem.Id, handbookPrice, -1, route.LoyaltyLevel, new List<BarterItem>(), 100);
                routed++;
            }

            logger.LogInformation(
                "[RZAutoAssort] {Routed} auto-routed, {Overridden} overridden, {Skipped} skipped.{RouteAll}",
                routed,
                overridden,
                skipped,
                config.ForceRouteAll ? " [ForceRouteAll ON]" : ""
            );
        }

        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // BuildModdedItemSet
    //
    // Loads vanilla_handbook.json from the mod root and returns the set of TPLs that are in the runtime
    // handbook but NOT in the vanilla one — i.e. items added by mods.
    //
    // vanilla_handbook.json must be kept up to date with each SPT patch. It is a plain copy of
    // Tarkov/Aki_Data/Server/database/templates/handbook.json from the vanilla SPT install.
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private HashSet<string> BuildModdedItemSet(HandbookBase handbook)
    {
        var vanillaPath = System.IO.Path.Combine(AppContext.BaseDirectory, "user", "mods", "RZAutoAssort", "vanilla_handbook.json");

        if (!System.IO.File.Exists(vanillaPath))
        {
            logger.LogWarning(
                "[RZAutoAssort] vanilla_handbook.json not found at '{Path}'. "
                    + "RouteModdedItemsOnly / ExcludeModdedItems will have no effect. "
                    + "Copy the vanilla handbook.json to this location to enable modded item detection.",
                vanillaPath
            );
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        HashSet<string> vanillaTpls;
        try
        {
            var raw = System.IO.File.ReadAllText(vanillaPath);
            using var doc = System.Text.Json.JsonDocument.Parse(raw);

            if (!doc.RootElement.TryGetProperty("Items", out var itemsEl))
            {
                logger.LogWarning("[RZAutoAssort] vanilla_handbook.json is empty or malformed — modded item detection disabled.");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            vanillaTpls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in itemsEl.EnumerateArray())
            {
                if (item.TryGetProperty("Id", out var idEl))
                    vanillaTpls.Add(idEl.GetString() ?? "");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[RZAutoAssort] Failed to parse vanilla_handbook.json: {Err}", ex.Message);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return handbook
            .Items.Select(i => i.Id.ToString())
            .Where(tpl => !vanillaTpls.Contains(tpl))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // InjectManualOffers
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private void InjectManualOffers(TraderAssort assort, List<TradeOffer> offers)
    {
        foreach (var (offer, index) in offers.Select((o, i) => (o, i)))
        {
            var itemId = GenerateId($"manual_{offer.ItemTpl}_{index}");

            assort.Items.Add(
                new Item
                {
                    Id = itemId,
                    Template = offer.ItemTpl,
                    ParentId = "hideout",
                    SlotId = "hideout",
                    Upd = new Upd
                    {
                        UnlimitedCount = offer.StackCount <= 0,
                        StackObjectsCount = offer.StackCount <= 0 ? 999999 : offer.StackCount,
                        BuyRestrictionMax = null,
                        BuyRestrictionCurrent = null,
                        Repairable = offer.Durability is > 0 and < 100
                            ? new UpdRepairable { MaxDurability = 100, Durability = offer.Durability }
                            : null,
                    },
                }
            );

            // Manual children defined in JSON
            foreach (var (child, childIndex) in offer.Children.Select((c, i) => (c, i)))
            {
                var childId = GenerateId($"manual_{offer.ItemTpl}_{index}_child_{childIndex}");
                assort.Items.Add(
                    new Item
                    {
                        Id = childId,
                        Template = child.ItemTpl,
                        ParentId = itemId,
                        SlotId = child.SlotId,
                        Upd = new Upd { StackObjectsCount = child.Count },
                    }
                );
            }

            // Auto-resolve required children not already covered by manual slots
            var manualSlots = offer.Children.Select(c => c.SlotId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            ResolveRequiredChildren(assort.Items, itemId, offer.ItemTpl, offer.Durability, manualSlots, depth: 0);

            assort.BarterScheme[itemId] = new List<List<BarterScheme>> { BuildPayment(offer.PriceRoubles, offer.BarterItems) };
            assort.LoyalLevelItems[itemId] = offer.LoyaltyLevel;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // InjectAutoOffer
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private void InjectAutoOffer(
        TraderAssort assort,
        MongoId tpl,
        int priceRoubles,
        int stackCount,
        int loyaltyLevel,
        List<BarterItem> barterItems,
        int durability
    )
    {
        var itemId = GenerateId($"auto_{tpl}");

        assort.Items.Add(
            new Item
            {
                Id = itemId,
                Template = tpl,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = stackCount <= 0,
                    StackObjectsCount = stackCount <= 0 ? 999999 : stackCount,
                    BuyRestrictionMax = null,
                    BuyRestrictionCurrent = null,
                },
            }
        );

        ResolveRequiredChildren(assort.Items, itemId, tpl, durability, new HashSet<string>(), depth: 0);

        assort.BarterScheme[itemId] = new List<List<BarterScheme>> { BuildPayment(priceRoubles, barterItems) };
        assort.LoyalLevelItems[itemId] = loyaltyLevel;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // BuildCategoryRouteMap
    //
    // Propagates parent routes down to all their child categories.
    // Also includes direct ParentId routes (base class IDs that appear as ParentId in handbook.Items but not in handbook.Categories).
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static Dictionary<MongoId, CategoryRoute> BuildCategoryRouteMap(List<HandbookCategory> categories, List<CategoryRoute> routes)
    {
        var directRoutes = routes.ToDictionary(r => r.CategoryId, StringComparer.OrdinalIgnoreCase);
        var catById = categories.ToDictionary(c => c.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<MongoId, CategoryRoute>();

        foreach (var cat in categories)
        {
            var route = FindRouteForCategory(cat.Id.ToString(), catById, directRoutes);
            if (route is not null)
                result[cat.Id] = route;
        }

        // Also register direct routes whose IDs are not in handbook.Categories
        foreach (var route in routes)
        {
            MongoId key = route.CategoryId;
            if (!result.ContainsKey(key))
                result[key] = route;
        }

        return result;
    }

    private static CategoryRoute? FindRouteForCategory(
        string categoryId,
        Dictionary<string, HandbookCategory> catById,
        Dictionary<string, CategoryRoute> directRoutes
    )
    {
        var current = categoryId;
        while (current is not null)
        {
            if (directRoutes.TryGetValue(current, out var route))
                return route;
            if (!catById.TryGetValue(current, out var cat))
                break;
            current = cat.ParentId?.ToString();
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // ResolveRequiredChildren
    //
    // Recursively injects required child items (armor plates, etc.) by reading the Slots of the TemplateItem from the DB.
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private void ResolveRequiredChildren(
        List<Item> items,
        MongoId parentId,
        MongoId parentTpl,
        int durability,
        HashSet<string> skipSlots,
        int depth
    )
    {
        if (depth > 5 || _itemTemplates is null)
        {
            return;
        }

        if (!_itemTemplates.TryGetValue(parentTpl, out var template))
        {
            return;
        }

        var slots = template.Properties?.Slots;
        if (slots is null)
            return;

        foreach (var slot in slots)
        {
            if (slot.Required != true || slot.Name is null)
                continue;

            if (skipSlots.Contains(slot.Name))
                continue;

            var childTpl = ResolveSlotDefaultTpl(slot);
            if (childTpl is null)
                continue;

            var childId = GenerateId($"{parentId}_{slot.Name}_{depth}");

            UpdRepairable? repairable = null;
            if (_itemTemplates.TryGetValue(childTpl.Value, out var childTemplate))
            {
                var maxDur = (int?)childTemplate.Properties?.Durability;
                if (maxDur is > 0)
                {
                    var ratio = durability <= 0 ? 1.0 : Math.Clamp(durability / 100.0, 0.0, 1.0);
                    repairable = new UpdRepairable { MaxDurability = maxDur.Value, Durability = (int)Math.Round(maxDur.Value * ratio) };
                }
            }

            items.Add(
                new Item
                {
                    Id = childId,
                    Template = childTpl.Value,
                    ParentId = parentId,
                    SlotId = slot.Name,
                    Upd = repairable is not null ? new Upd { Repairable = repairable } : null,
                }
            );

            ResolveRequiredChildren(items, childId, childTpl.Value, durability, new HashSet<string>(), depth + 1);
        }
    }

    private static MongoId? ResolveSlotDefaultTpl(Slot slot)
    {
        var filters = slot.Properties?.Filters;
        if (filters is null)
            return null;

        foreach (var filter in filters)
        {
            if (filter.Plate is not null && filter.Plate.Value != default)
                return filter.Plate.Value;

            if (filter.Filter is not null && filter.Filter.Count > 0)
                return filter.Filter.First();
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static List<BarterScheme> BuildPayment(int priceRoubles, List<BarterItem> barterItems)
    {
        var payment = new List<BarterScheme>();

        if (priceRoubles > 0)
            payment.Add(new BarterScheme { Template = ItemTpl.MONEY_ROUBLES, Count = priceRoubles });

        foreach (var b in barterItems)
            payment.Add(new BarterScheme { Template = b.ItemTpl, Count = b.Count });

        return payment.Count > 0
            ? payment
            : new List<BarterScheme>
            {
                new() { Template = ItemTpl.MONEY_ROUBLES, Count = 0 },
            };
    }

    private static MongoId GenerateId(string seed)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash).ToLower()[..24];
    }
}
