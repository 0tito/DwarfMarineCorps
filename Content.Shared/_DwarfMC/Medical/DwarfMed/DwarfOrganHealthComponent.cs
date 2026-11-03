using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfOrganHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public DwarfOrganType OrganType;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxDamage = 100;

    [DataField, AutoNetworkedField]
    public FixedPoint2 RuptureThreshold = 100;

    [DataField, AutoNetworkedField]
    public bool Vital;

    [DataField, AutoNetworkedField]
    public bool Ruptured;
}

public enum DwarfOrganType : byte
{
    Brain,
    Eyes,
    Heart,
    Lungs,
    Stomach,
    Liver,
    Kidneys,
}
