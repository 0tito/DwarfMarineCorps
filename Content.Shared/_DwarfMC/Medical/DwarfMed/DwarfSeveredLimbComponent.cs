using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfSeveredLimbComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalBody;

    [DataField, AutoNetworkedField]
    public BodyZone Zone;
}
