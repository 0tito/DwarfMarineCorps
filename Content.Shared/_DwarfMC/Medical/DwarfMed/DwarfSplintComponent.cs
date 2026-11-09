using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfSplintComponent : Component
{
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(3);
}
