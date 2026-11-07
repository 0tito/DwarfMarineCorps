using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfBodyPartComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Dismemberable = true;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxLocalDamage = 300;

    [DataField, AutoNetworkedField]
    public FixedPoint2 SlashDamage;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BruteDamage;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BurnDamage;

    [DataField, AutoNetworkedField]
    public FixedPoint2 SlashThreshold = 80;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BruteThreshold = 120;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BurnThreshold = 300;

    [DataField, AutoNetworkedField]
    public bool DismemberAttempted;

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

    [DataField, AutoNetworkedField]
    public float StumpBleedRate = 0.3f;
}
