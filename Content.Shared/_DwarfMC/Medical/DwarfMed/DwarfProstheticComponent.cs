using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfProstheticComponent : Component
{
    [DataField]
    public Color LimbColor = Color.FromHex("#3A3A3A");
}
