using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Intel.Tech;

/// <summary>
/// Recipes on a lathe that only appear once they have been unlocked through the intel tech tree.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IntelLatheRecipesComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<LatheRecipePrototype>> Recipes = new();
}
