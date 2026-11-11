using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[DataRecord, Serializable, NetSerializable]
public readonly record struct DwarfBodyPartScan(
    string SlotId,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    FixedPoint2 SlashDamage,
    FixedPoint2 BruteDamage,
    FixedPoint2 BurnDamage,
    FixedPoint2 SlashThreshold,
    FixedPoint2 BruteThreshold,
    FixedPoint2 BurnThreshold,
    bool Bleeding,
    bool StumpBleeding,
    bool Severed,
    bool Fractured,
    bool Splinted,
    bool InternalBleeding);
