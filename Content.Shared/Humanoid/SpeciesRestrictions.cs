using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

/// <summary>
/// Central allowlist of playable species for this dwarf-only fork.
/// Keeping this in code prevents upstream merges that add new species
/// prototypes from silently making them playable.
/// </summary>
public static class SpeciesRestrictions
{
    public static readonly ProtoId<SpeciesPrototype> AllowedSpecies = "Dwarf";

    public static bool IsAllowed(SpeciesPrototype species)
    {
        return species.ID == AllowedSpecies.Id;
    }

    public static bool IsAllowed(string species)
    {
        return species == AllowedSpecies.Id;
    }
}
