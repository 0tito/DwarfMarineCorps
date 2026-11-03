using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfBodyPartComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Dismemberable = true;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DismemberThreshold = 40;

    [DataField, AutoNetworkedField]
    public FixedPoint2 FractureThreshold = 20;

    [DataField, AutoNetworkedField]
    public FixedPoint2 OrganDamageThreshold = 25;

    [DataField, AutoNetworkedField]
    public float BleedMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float FractureChanceMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float InternalBleedChanceMultiplier = 1f;
}
