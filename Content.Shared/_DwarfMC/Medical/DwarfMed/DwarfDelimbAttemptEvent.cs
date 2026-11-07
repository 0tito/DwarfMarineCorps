using Content.Shared.Damage;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[ByRefEvent]
public struct DwarfDelimbAttemptEvent
{
    public EntityUid Body;
    public EntityUid Part;
    public DwarfSeverReason Reason;
    public DamageSpecifier Damage;
    public EntityUid? Origin;
    public EntityUid? Tool;
    public BodyZone Zone;
    public float Chance;
    public bool Cancelled;
    public bool Forced;
}
