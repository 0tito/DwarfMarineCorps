using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Random;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public sealed class BodyZoneSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public bool TryGetPartForZone(
        EntityUid body,
        BodyZone zone,
        out EntityUid part,
        BodyComponent? bodyComponent = null,
        IReadOnlyDictionary<BodyPartType, float>? weights = null)
    {
        part = default;

        if (!Resolve(body, ref bodyComponent, false))
            return false;

        if (TryGetZonePartType(zone, out var type, out var symmetry) &&
            TryGetAttachedPart(body, type, symmetry, out part, bodyComponent))
        {
            return true;
        }

        foreach (var fallback in GetFallbackZones(zone))
        {
            if (!TryGetZonePartType(fallback, out type, out symmetry))
                continue;

            if (TryGetAttachedPart(body, type, symmetry, out part, bodyComponent))
                return true;
        }

        return TryGetWeightedRandomPart(body, weights, out part, bodyComponent);
    }

    public bool TryGetWeightedRandomPart(
        EntityUid body,
        IReadOnlyDictionary<BodyPartType, float>? weights,
        out EntityUid part,
        BodyComponent? bodyComponent = null)
    {
        part = default;

        if (!Resolve(body, ref bodyComponent, false))
            return false;

        var parts = _body.GetBodyChildren(body, bodyComponent).ToArray();
        if (parts.Length == 0)
            return false;

        if (weights == null || weights.Count == 0)
        {
            part = _random.Pick(parts).Id;
            return true;
        }

        var counts = new Dictionary<BodyPartType, int>();
        foreach (var (_, bodyPart) in parts)
        {
            counts[bodyPart.PartType] = counts.GetValueOrDefault(bodyPart.PartType) + 1;
        }

        var total = 0f;
        foreach (var (_, bodyPart) in parts)
        {
            if (!weights.TryGetValue(bodyPart.PartType, out var weight) || weight <= 0)
                continue;

            total += weight / counts[bodyPart.PartType];
        }

        if (total <= 0)
        {
            part = _random.Pick(parts).Id;
            return true;
        }

        var roll = _random.NextFloat(total);
        foreach (var (id, bodyPart) in parts)
        {
            if (!weights.TryGetValue(bodyPart.PartType, out var weight) || weight <= 0)
                continue;

            roll -= weight / counts[bodyPart.PartType];
            if (roll > 0)
                continue;

            part = id;
            return true;
        }

        part = _random.Pick(parts).Id;
        return true;
    }



    public bool TryGetAttachedPartExact(
        EntityUid body,
        BodyZone zone,
        out EntityUid part,
        out BodyPartComponent? bodyPart,
        BodyComponent? bodyComponent = null)
    {
        part = default;
        bodyPart = null;

        if (!Resolve(body, ref bodyComponent, false) ||
            !TryGetZonePartType(zone, out var type, out var symmetry) ||
            !TryGetAttachedPart(body, type, symmetry, out part, bodyComponent))
        {
            return false;
        }

        bodyPart = Comp<BodyPartComponent>(part);
        return true;
    }

    public static bool TryGetZoneForPart(BodyPartComponent part, out BodyZone zone)
    {
        switch (part.PartType)
        {
            case BodyPartType.Head:
                zone = BodyZone.Head;
                return true;
            case BodyPartType.Torso:
                zone = BodyZone.Chest;
                return true;
            case BodyPartType.Groin:
                zone = BodyZone.Groin;
                return true;
            case BodyPartType.Arm:
                zone = part.Symmetry == BodyPartSymmetry.Left ? BodyZone.LeftArm : BodyZone.RightArm;
                return true;
            case BodyPartType.Hand:
                zone = part.Symmetry == BodyPartSymmetry.Left ? BodyZone.LeftHand : BodyZone.RightHand;
                return true;
            case BodyPartType.Leg:
                zone = part.Symmetry == BodyPartSymmetry.Left ? BodyZone.LeftLeg : BodyZone.RightLeg;
                return true;
            case BodyPartType.Foot:
                zone = part.Symmetry == BodyPartSymmetry.Left ? BodyZone.LeftFoot : BodyZone.RightFoot;
                return true;
            default:
                zone = BodyZone.Chest;
                return false;
        }
    }

    public static bool TryGetZonePartType(
        BodyZone zone,
        out BodyPartType type,
        out BodyPartSymmetry symmetry)
    {
        symmetry = BodyPartSymmetry.None;

        switch (zone)
        {
            case BodyZone.Head:
                type = BodyPartType.Head;
                return true;
            case BodyZone.Chest:
                type = BodyPartType.Torso;
                return true;
            case BodyZone.Groin:
                type = BodyPartType.Groin;
                return true;
            case BodyZone.LeftArm:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyZone.RightArm:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyZone.LeftHand:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyZone.RightHand:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyZone.LeftLeg:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyZone.RightLeg:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case BodyZone.LeftFoot:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case BodyZone.RightFoot:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Right;
                return true;
            default:
                type = BodyPartType.Other;
                return false;
        }
    }

    public static IEnumerable<BodyZone> GetFallbackZones(BodyZone zone)
    {
        switch (zone)
        {
            case BodyZone.LeftHand:
                yield return BodyZone.LeftArm;
                yield return BodyZone.Chest;
                break;
            case BodyZone.RightHand:
                yield return BodyZone.RightArm;
                yield return BodyZone.Chest;
                break;
            case BodyZone.LeftFoot:
                yield return BodyZone.LeftLeg;
                yield return BodyZone.Groin;
                yield return BodyZone.Chest;
                break;
            case BodyZone.RightFoot:
                yield return BodyZone.RightLeg;
                yield return BodyZone.Groin;
                yield return BodyZone.Chest;
                break;
            case BodyZone.LeftArm:
            case BodyZone.RightArm:
            case BodyZone.LeftLeg:
            case BodyZone.RightLeg:
                yield return BodyZone.Chest;
                break;
            case BodyZone.Head:
            case BodyZone.Groin:
                yield return BodyZone.Chest;
                break;
        }
    }

    private bool TryGetAttachedPart(
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntityUid part,
        BodyComponent bodyComponent)
    {
        foreach (var (id, bodyPart) in _body.GetBodyChildren(body, bodyComponent))
        {
            if (bodyPart.PartType != type)
                continue;

            if (symmetry != BodyPartSymmetry.None && bodyPart.Symmetry != symmetry)
                continue;

            part = id;
            return true;
        }

        part = default;
        return false;
    }
}
