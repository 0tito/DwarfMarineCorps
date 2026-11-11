using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfBodyPartConditionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Fractured;

    [DataField, AutoNetworkedField]
    public bool Splinted;

    [DataField, AutoNetworkedField]
    public bool InternalBleeding;

    [DataField, AutoNetworkedField]
    public float InternalBleedRate;

    [DataField, AutoNetworkedField]
    public bool StumpBleeding;

    [DataField, AutoNetworkedField]
    public float StumpBleedRate;

    [DataField, AutoNetworkedField]
    public bool Severed;
}
