using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public sealed class DwarfBodyPartScanSystem : EntitySystem
{
    [Dependency] private readonly BodyZoneSystem _bodyZones = default!;

    public List<DwarfBodyPartScan> Build(EntityUid target)
    {
        var result = new List<DwarfBodyPartScan>();
        if (!HasComp<DwarfAnatomyComponent>(target))
            return result;

        foreach (var zone in Enum.GetValues<BodyZone>())
        {
            if (_bodyZones.TryGetAttachedPartExact(target, zone, out var part, out _) &&
                TryComp<BodyPartComponent>(part, out var bodyPart) &&
                TryComp<DwarfBodyPartComponent>(part, out var damage))
            {
                var condition = CompOrNull<DwarfBodyPartConditionComponent>(part);
                result.Add(new DwarfBodyPartScan(
                    zone.ToString(),
                    bodyPart.PartType,
                    bodyPart.Symmetry,
                    damage.SlashDamage,
                    damage.BruteDamage,
                    damage.BurnDamage,
                    damage.SlashThreshold,
                    damage.BruteThreshold,
                    damage.BurnThreshold,
                    condition?.StumpBleeding ?? false,
                    condition?.StumpBleeding ?? false,
                    condition?.Severed ?? false));
            }
            else
            {
                result.Add(new DwarfBodyPartScan(
                    zone.ToString(),
                    BodyPartType.Other,
                    BodyPartSymmetry.None,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    false,
                    false,
                    true));
            }
        }

        return result;
    }
}
