// RemzDNB - 2026

using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace RZAutoAssort;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.rz.autoassort";
    public override string Name { get; init; } = "RZAutoAssort";
    public override string Author { get; init; } = "RemzDNB";

    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");

    /// <summary>
    /// Compatible SPT version range.
    /// "~4.0.0" = any 4.0.x version.
    /// "^4.0.0" = any 4.x.x version.
    /// </summary>
    public override Range SptVersion { get; init; } = new("~4.0.0");

    public override string License { get; init; } = "MIT";
    public override List<string>? Contributors { get; init; } = null;
    public override List<string>? Incompatibilities { get; init; } = null;
    public override Dictionary<string, Range>? ModDependencies { get; init; } = null;
    public override string? Url { get; init; } = null;
    public override bool? IsBundleMod { get; init; } = false;
}
