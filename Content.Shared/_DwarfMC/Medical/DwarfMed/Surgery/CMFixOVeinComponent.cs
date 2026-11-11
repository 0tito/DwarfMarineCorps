using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMFixOVeinComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "Fix-O-Vein";
}
