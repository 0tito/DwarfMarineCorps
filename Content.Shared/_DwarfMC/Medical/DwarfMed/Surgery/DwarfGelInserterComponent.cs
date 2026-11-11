using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class DwarfGelInserterComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "gel inserter";

    [DataField]
    public string SolutionName = "gel";

    [DataField]
    public FixedPoint2 ReagentPerUse = 5;

    [DataField]
    public float WrongOrganDamage = 5f;

    [DataField]
    public float WrongToxinDamage = 5f;
}
