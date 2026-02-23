// RemzDNB - 2026

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services;

namespace RZAutoAssort;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class ProfilesPatcher(DatabaseService databaseService, ConfigLoader configLoader) : IOnLoad
{
    public Task OnLoad()
    {
        var config = configLoader.Load<AutoAssortConfig>(AutoAssortConfig.FileName);
        var profiles = databaseService.GetProfileTemplates();
        var allItems = databaseService.GetTables().Templates?.Items?.Keys.ToHashSet();

        if (!config.AllItemsExamined)
        {
            return Task.CompletedTask;
        }

        // StaticBlacklist items are broken/non-functional — they should never appear as identified in the
        // encyclopedia, even when AllItemsExamined is true.
        var staticBlacklist = new HashSet<MongoId>(config.StaticBlacklist.Select(tpl => new MongoId(tpl)));

        foreach (var (_, edition) in profiles)
        {
            foreach (var side in new[] { edition.Usec, edition.Bear })
            {
                var character = side?.Character;
                if (character is null)
                {
                    continue;
                }

                character.Encyclopedia ??= new Dictionary<MongoId, bool>();
                foreach (var tpl in allItems!)
                {
                    if (staticBlacklist.Contains(tpl))
                    {
                        continue;
                    }

                    character.Encyclopedia[tpl] = true;
                }
            }
        }

        return Task.CompletedTask;
    }
}
