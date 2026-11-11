using Content.Shared._DwarfMC.Medical.DwarfMed;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryOrganDamageConditionComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepRepairOrgansEffectComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepBoneGelEffectComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryOrganPresentConditionComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryOrganMissingConditionComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepRemoveOrganEffectComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepInsertOrganEffectComponent : Component
{
    [DataField]
    public DwarfOrganType? OrganType;
}
