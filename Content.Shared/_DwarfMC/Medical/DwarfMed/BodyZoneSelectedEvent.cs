using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public sealed class BodyZoneSelectedEvent(BodyZone zone) : EntityEventArgs
{
    public readonly BodyZone Zone = zone;
}
