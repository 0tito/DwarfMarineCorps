using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public sealed partial class DwarfStumpPackingDoAfterEvent : SimpleDoAfterEvent
{
    public readonly BodyZone Zone;

    public DwarfStumpPackingDoAfterEvent(BodyZone zone)
    {
        Zone = zone;
    }
}
