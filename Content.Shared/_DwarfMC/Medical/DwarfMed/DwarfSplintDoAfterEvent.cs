using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public sealed partial class DwarfSplintDoAfterEvent : SimpleDoAfterEvent
{
    public readonly BodyZone Zone;

    public DwarfSplintDoAfterEvent(BodyZone zone)
    {
        Zone = zone;
    }
}
