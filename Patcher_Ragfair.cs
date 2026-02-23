// RemzDNB - 2026

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace RZAutoAssort;

// Removes traders with no assorts from the ragfair config to prevent ragfair from spamming errors when trying to generate offers for
// empty traders. For this to work correctly, all patchers that modify trader assorts must run at RagfairCallbacks - 2 or earlier.

[Injectable(TypePriority = OnLoadOrder.RagfairCallbacks - 1)]
public class RagfairTradersPatcher(DatabaseService databaseService, ConfigServer configServer) : IOnLoad
{
    public Task OnLoad()
    {
        var traders = databaseService.GetTraders();
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();

        foreach (var (id, trader) in traders)
        {
            if (trader.Assort?.Items is null || trader.Assort.Items.Count == 0)
            {
                ragfairConfig.Traders.Remove(id.ToString());
            }
        }

        return Task.CompletedTask;
    }
}
