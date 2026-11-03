using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfBodyZoneTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public DwarfBodyZone SelectedZone = DwarfBodyZone.Random;
}
