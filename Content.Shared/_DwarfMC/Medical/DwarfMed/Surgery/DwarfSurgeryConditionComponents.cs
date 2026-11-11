using Content.Shared._DwarfMC.Medical.DwarfMed;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryFracturedConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryInternalBleedingConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStumpBleedingConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfBoneGelledComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryMissingLimbConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryAmputatableConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfProstheticInstalledComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryProstheticSlotConditionComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfInternalBleedingRepairedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfStumpSealedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfLimbReattachedComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfOrganRepairedComponent : Component
{
    [DataField]
    public HashSet<DwarfOrganType> Repaired = [];
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfOrganRemovedComponent : Component
{
    [DataField]
    public HashSet<DwarfOrganType> Removed = [];
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfOrganInsertedComponent : Component
{
    [DataField]
    public HashSet<DwarfOrganType> Inserted = [];
}
