using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Intel.Tech;

/// <summary>
/// Stores the lathe recipes that have been unlocked through the intel tech tree for this round.
/// </summary>
[RegisterComponent]
public sealed partial class IntelUnlockedRecipesComponent : Component
{
    [DataField]
    public HashSet<ProtoId<LatheRecipePrototype>> Recipes = new();
}
