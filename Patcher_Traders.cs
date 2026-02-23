// RemzDNB - 2026

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Services;

namespace RZAutoAssort;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TradersPatcher(DatabaseService databaseService, ConfigLoader configLoader) : IOnLoad
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
