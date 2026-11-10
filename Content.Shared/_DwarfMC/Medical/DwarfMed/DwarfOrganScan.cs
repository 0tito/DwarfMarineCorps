using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[DataRecord, Serializable, NetSerializable]
public readonly record struct DwarfOrganScan(
    string OrganSlot,
    string OrganName,
    DwarfOrganType Type,
    FixedPoint2 Damage,
    FixedPoint2 MaxDamage,
    bool Ruptured,
    bool Vital);
