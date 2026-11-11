using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyZoneTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public BodyZone SelectedZone = BodyZone.Chest;
}
