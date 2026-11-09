using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepRepairFractureEffectComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepStopInternalBleedingEffectComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepSealStumpEffectComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSurgeryStepReattachLimbEffectComponent : Component;
