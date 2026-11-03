using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfAnatomyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
