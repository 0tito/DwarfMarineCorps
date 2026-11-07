namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[ByRefEvent]
public struct DwarfSeverRequestEvent
{
    public EntityUid Body;
    public EntityUid Part;
    public DwarfSeverReason Reason;
    public EntityUid? Origin;
    public EntityUid? Tool;
    public bool Handled;
}
