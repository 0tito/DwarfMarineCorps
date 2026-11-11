using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public sealed partial class DwarfLimbTreatmentDoAfterEvent : SimpleDoAfterEvent
{
    public readonly BodyZone Zone;
    public readonly bool Brute;

    public DwarfLimbTreatmentDoAfterEvent(BodyZone zone, bool brute)
    {
        Zone = zone;
        Brute = brute;
    }
}
