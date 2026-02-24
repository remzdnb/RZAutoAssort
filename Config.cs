// RemzDNB - 2026

namespace RZAutoAssort;

// ─────────────────────────────────────────────────────────────────────────────
// Shared primitives
// ─────────────────────────────────────────────────────────────────────────────

public class BarterItem
{
    public string ItemTpl { get; set; } = "";
    public int Count { get; set; } = 1;
}

public class ChildItem
{
    public string ItemTpl { get; set; } = "";
    public string SlotId { get; set; } = "";
    public int Count { get; set; } = 1;
}

// ─────────────────────────────────────────────────────────────────────────────
// Trader IDs
// ─────────────────────────────────────────────────────────────────────────────

public static class TraderIds
{
    public const string Prapor = "54cb50c76803fa8b248b4571";
    public const string Therapist = "54cb57776803fa99248b456e";
    public const string Fence = "579dc571d53a0658a154fbec";
    public const string Skier = "58330581ace78e27b8b10cee";
    public const string Peacekeeper = "5935c25fb3acc3127c3d8cd9";
    public const string Mechanic = "5a7c2eca46aef81a7ca2145d";
    public const string Ragman = "5ac3b934156ae10c4430e83c";
    public const string Jaeger = "5c0647fdd443bc2504c2d371";
    public const string Caretaker = "638f541a29ffd1183d187f57";
    public const string Btr = "656f0f98d80a697f855d34b1";
    public const string Arena = "6617beeaa9cfa777ca915b7c";
    public const string Storyteller = "6864e812f9fe664cb8b8e152";

    public static string? FromName(string name)
    {
        return typeof(TraderIds).GetField(name)?.GetValue(null) as string;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Manual offers
// ─────────────────────────────────────────────────────────────────────────────

public class TradeOffer
{
    public string ItemTpl { get; set; } = "";
    public int StackCount { get; set; } = 1;
    public int LoyaltyLevel { get; set; } = 1;
    public int Durability { get; set; } = 100;
    public int PriceRoubles { get; set; } = 0;
    public List<ChildItem> Children { get; set; } = new();
    public List<BarterItem> BarterItems { get; set; } = new();
}

public class ManualTraderOffers
{
    public string Id { get; set; } = ""; // trader id
    public List<TradeOffer> Offers { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Auto-routing
// ─────────────────────────────────────────────────────────────────────────────

public class CategoryRoute
{
    public bool Enabled { get; set; } = true;
    public string CategoryId { get; set; } = "";
    public string TraderName { get; set; } = "";
    public double PriceMultiplier { get; set; } = 1.0;
    public int LoyaltyLevel { get; set; } = 1;
}

public class AutoTradeOverride
{
    public string ItemTpl { get; set; } = "";
    public string TraderName { get; set; } = "";
    public int PriceRoubles { get; set; } = 0;
    public double PriceMultiplier { get; set; } = 1.0;
    public int LoyaltyLevel { get; set; } = 1;
    public int StackCount { get; set; } = -1;
    public List<BarterItem> BarterItems { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Trader sell config
// ─────────────────────────────────────────────────────────────────────────────

public enum ItemSellMode
{
    Default,
    Disabled,
    Categories,
    AllWithBlacklist,
}

public class TraderSellConfig
{
    public ItemSellMode Mode { get; set; } = ItemSellMode.Disabled;
    public List<string> Categories { get; set; } = new();
    public List<string> Blacklist { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Config
// ─────────────────────────────────────────────────────────────────────────────

public class AutoAssortConfig
{
    public const string FileName = "userConfig.json";

    public bool ClearDefaultAssorts { get; set; } = true;
    public bool EnableAutoRouting { get; set; } = true;
    public bool ForceRouteAll { get; set; } = false;
    public bool RouteModdedItemsOnly { get; set; } = false;
    public bool RouteVanillaItemsOnly { get; set; } = false;
    public bool UseStaticBlacklist { get; set; } = true;
    public bool UseUserBlacklist { get; set; } = true;
    public bool EnableManualOffers { get; set; } = true;
    public bool EnableOverrides { get; set; } = true;
    public bool AllItemsExamined { get; set; } = false;
    public bool UnlockAllTraders { get; set; } = false;
    public string? FallbackTrader { get; set; } = null;
    public bool EnableSellConfigs { get; set; } = true;
    public bool EnableDevMode { get; set; } = false;

    public List<string> StaticBlacklist { get; set; } = new();
    public List<string> UserBlacklist { get; set; } = new();

    public List<ManualTraderOffers> ManualOffers { get; set; } = new();
    public List<CategoryRoute> CategoryRoutes { get; set; } = new();
    public List<AutoTradeOverride> Overrides { get; set; } = new();
    public Dictionary<string, TraderSellConfig> TraderSellConfigs { get; set; } = new();
}
