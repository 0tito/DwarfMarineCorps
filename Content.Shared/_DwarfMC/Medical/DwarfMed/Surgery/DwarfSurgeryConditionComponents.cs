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
