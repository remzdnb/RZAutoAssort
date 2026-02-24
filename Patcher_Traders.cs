// RemzDNB - 2026

using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;

namespace RZAutoAssort;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TradersUnlockPatcher(DatabaseService databaseService, ConfigLoader configLoader) : IOnLoad
{
    public Task OnLoad()
    {
        var config = configLoader.Load<AutoAssortConfig>(AutoAssortConfig.FileName);
        if (!config.UnlockAllTraders)
        {
            return Task.CompletedTask;
        }

        var traders = config.UnlockAllTraders ? databaseService.GetTraders() : null;
        if (traders is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (_, trader) in traders)
        {
            trader.Base.UnlockedByDefault = true;
        }

        return Task.CompletedTask;
    }
}

// Not sure about the load order yet, setting it late for now to make it work for modded items ?
[Injectable(TypePriority = OnLoadOrder.RagfairCallbacks - 1)]
public class TradersSellPatcher(ILogger<TradersSellPatcher> logger, DatabaseService databaseService, ConfigLoader configLoader) : IOnLoad
{
    public Task OnLoad()
    {
        var config = configLoader.Load<AutoAssortConfig>(AutoAssortConfig.FileName);
        if (!config.EnableSellConfigs || config.TraderSellConfigs.Count == 0)
        {
            return Task.CompletedTask;
        }

        var traders = databaseService.GetTraders();
        var handbookTpls = databaseService.GetTables().Templates?.Handbook?.Items.Select(i => i.Id).ToHashSet();

        foreach (var (traderName, sellConfig) in config.TraderSellConfigs)
        {
            var traderId = TraderIds.FromName(traderName);
            if (traderId is null || !traders.TryGetValue(traderId, out var trader))
            {
                logger.LogWarning("[RZAutoAssort] SellPatcher: trader '{Name}' not found -- skipping.", traderName);
                continue;
            }

            if (sellConfig.Mode == ItemSellMode.Default)
            {
                continue;
            }

            trader.Base.ItemsBuy = BuildItemBuyData(traderName, sellConfig, handbookTpls);
        }

        return Task.CompletedTask;
    }

    private ItemBuyData BuildItemBuyData(string traderName, TraderSellConfig sellConfig, HashSet<MongoId>? handbookTpls)
    {
        switch (sellConfig.Mode)
        {
            case ItemSellMode.Disabled:
                //logger.LogInformation("[RZAutoAssort] {Trader}: selling disabled.", traderName);
                return new ItemBuyData { Category = new HashSet<MongoId>(), IdList = new HashSet<MongoId>() };

            case ItemSellMode.Categories:
                var categories = sellConfig.Categories.Select(c => new MongoId(c)).ToHashSet();
                /*logger.LogInformation(
                    "[RZAutoAssort] {Trader}: selling enabled for {Count} category/categories.",
                    traderName,
                    categories.Count
                );*/
                return new ItemBuyData { Category = categories, IdList = new HashSet<MongoId>() };

            case ItemSellMode.AllWithBlacklist:
                if (handbookTpls is null)
                {
                    logger.LogWarning("[RZAutoAssort] {Trader}: handbook is null, cannot build sell whitelist.", traderName);
                    return new ItemBuyData { Category = new HashSet<MongoId>(), IdList = new HashSet<MongoId>() };
                }

                var blacklist = sellConfig.Blacklist.Select(t => new MongoId(t)).ToHashSet();

                var idList = handbookTpls.Where(tpl => !blacklist.Contains(tpl)).ToHashSet();

                /*logger.LogInformation(
                    "[RZAutoAssort] {Trader}: selling all handbook items ({Total} total, {Blocked} blocked).",
                    traderName,
                    handbookTpls.Count,
                    blacklist.Count
                );*/
                return new ItemBuyData { Category = new HashSet<MongoId>(), IdList = idList };

            default:
                logger.LogWarning("[RZAutoAssort] {Trader}: unknown sell mode '{Mode}' -- disabling.", traderName, sellConfig.Mode);
                return new ItemBuyData { Category = new HashSet<MongoId>(), IdList = new HashSet<MongoId>() };
        }
    }
}
