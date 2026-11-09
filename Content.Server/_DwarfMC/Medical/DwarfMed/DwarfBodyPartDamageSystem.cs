using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfBodyPartDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly BodyZoneSystem _bodyZone = default!;
    [Dependency] private readonly DwarfFractureSystem _fracture = default!;
    [Dependency] private readonly DwarfInternalBleedingSystem _internalBleeding = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<DwarfAnatomyComponent> ent, ref DamageChangedEvent args)
    {
        if (!_net.IsServer || !ent.Comp.Enabled || args.DamageDelta == null)
            return;

        if (!args.DamageIncreased)
        {
            HealLimb(ent, args);
            return;
        }

        var slash = DwarfLimbDamage.GetDamage(args.DamageDelta, DwarfLimbDamage.SlashTypes);
        var brute = DwarfLimbDamage.GetDamage(args.DamageDelta, DwarfLimbDamage.BruteTypes);
        var burn = DwarfLimbDamage.GetDamage(args.DamageDelta, DwarfLimbDamage.BurnTypes);
        var heat = DwarfLimbDamage.GetDamage(args.DamageDelta, "Heat");

        if (heat > FixedPoint2.Zero && HasComp<OnFireComponent>(ent.Owner))
        {
            foreach (var (id, part) in _body.GetBodyChildren(ent.Owner))
            {
                if (!TryComp<DwarfBodyPartComponent>(id, out var bodyPart))
                    continue;

                if (!BodyZoneSystem.TryGetZoneForPart(part, out var fireZone))
                    fireZone = BodyZone.Chest;

                ApplyLimbDamage(ent, id, bodyPart, FixedPoint2.Zero, FixedPoint2.Zero, heat,
                    DwarfSeverReason.Fire, args, fireZone);
            }

            burn -= heat;
        }

        if (slash == FixedPoint2.Zero &&
            brute == FixedPoint2.Zero &&
            burn == FixedPoint2.Zero)
        {
            return;
        }

        BodyZone zone;
        EntityUid partId;
        if (args.Origin is { } origin &&
            TryComp<BodyZoneTargetComponent>(origin, out var target))
        {
            zone = target.SelectedZone;
            if (!_bodyZone.TryGetPartForZone(ent.Owner, zone, out partId, weights: ent.Comp.HitWeights))
                return;
        }
        else
        {
            if (!_bodyZone.TryGetWeightedRandomPart(ent.Owner, ent.Comp.HitWeights, out partId))
                return;

            zone = BodyZone.Chest;
        }

        if (!TryComp<DwarfBodyPartComponent>(partId, out var selectedPart))
            return;

        if (!BodyZoneSystem.TryGetZoneForPart(Comp<BodyPartComponent>(partId), out zone))
            zone = BodyZone.Chest;

        ApplyLimbDamage(ent, partId, selectedPart, slash, brute, burn,
            DwarfSeverReason.Damage, args, zone);
    }

    private void ApplyLimbDamage(
        Entity<DwarfAnatomyComponent> ent,
        EntityUid partId,
        DwarfBodyPartComponent bodyPart,
        FixedPoint2 slash,
        FixedPoint2 brute,
        FixedPoint2 burn,
        DwarfSeverReason reason,
        DamageChangedEvent args,
        BodyZone zone)
    {
        var share = ent.Comp.LimbDamageShare;
        var localSlash = slash * share;
        var localBrute = brute * share;
        var changed = false;

        if (slash > FixedPoint2.Zero)
        {
            bodyPart.SlashDamage = DwarfLimbDamage.AddCapped(bodyPart.SlashDamage, localSlash, bodyPart.MaxLocalDamage);
            changed = true;
        }

        if (brute > FixedPoint2.Zero)
        {
            bodyPart.BruteDamage = DwarfLimbDamage.AddCapped(bodyPart.BruteDamage, localBrute, bodyPart.MaxLocalDamage);
            changed = true;
        }

        if (burn > FixedPoint2.Zero)
        {
            bodyPart.BurnDamage = DwarfLimbDamage.AddCapped(bodyPart.BurnDamage, burn * share, bodyPart.MaxLocalDamage);
            changed = true;
        }

        if (!changed)
            return;

        Dirty(partId, bodyPart);

        if (reason != DwarfSeverReason.Fire)
        {
            var localTrauma = localSlash + localBrute;
            if (localTrauma > FixedPoint2.Zero)
                _fracture.TryFracture(ent, partId, bodyPart, localTrauma);

            if (localBrute > FixedPoint2.Zero)
                _internalBleeding.TryCauseInternalBleedingFromBrute(ent, partId, bodyPart, localBrute);
        }

        if (bodyPart.DismemberAttempted ||
            !bodyPart.Dismemberable ||
            !DwarfLimbDamage.HasReachedThreshold(bodyPart))
        {
            return;
        }

        bodyPart.DismemberAttempted = true;
        Dirty(partId, bodyPart);

        var delimbReason = reason;
        if (reason != DwarfSeverReason.Fire && DwarfLimbDamage.IsBurnThresholdReached(bodyPart))
            delimbReason = DwarfSeverReason.Fire;

        var attempt = new DwarfDelimbAttemptEvent
        {
            Body = ent.Owner,
            Part = partId,
            Reason = delimbReason,
            Damage = args.DamageDelta!,
            Origin = args.Origin,
            Tool = args.Tool,
            Zone = zone,
            Chance = 0,
            Forced = false,
        };

        RaiseLocalEvent(ent.Owner, ref attempt);

        if (attempt.Cancelled)
            return;

        if (!attempt.Forced && (attempt.Chance <= 0 || !_random.Prob(attempt.Chance)))
            return;

        var request = new DwarfSeverRequestEvent
        {
            Body = ent.Owner,
            Part = partId,
            Reason = delimbReason,
            Origin = args.Origin,
            Tool = args.Tool,
        };

        RaiseLocalEvent(ent.Owner, ref request);
    }

    private void HealLimb(Entity<DwarfAnatomyComponent> ent, DamageChangedEvent args)
    {
        if (args.DamageDelta == null ||
            args.Origin is not { } origin ||
            !TryComp<BodyZoneTargetComponent>(origin, out var target))
        {
            return;
        }

        if (!_bodyZone.TryGetPartForZone(ent.Owner, target.SelectedZone, out var part, weights: ent.Comp.HitWeights) ||
            !TryComp<DwarfBodyPartComponent>(part, out var bodyPart))
        {
            return;
        }

        var bruteHeal = -DwarfLimbDamage.GetDamageRaw(args.DamageDelta, DwarfLimbDamage.AllBruteTypes);
        var burnHeal = -DwarfLimbDamage.GetDamageRaw(args.DamageDelta, DwarfLimbDamage.BurnTypes);
        if (bruteHeal <= FixedPoint2.Zero && burnHeal <= FixedPoint2.Zero)
            return;

        if (bruteHeal > FixedPoint2.Zero)
            DwarfLimbDamage.HealBrute(bodyPart, bruteHeal * ent.Comp.LimbHealShare);

        if (burnHeal > FixedPoint2.Zero)
            DwarfLimbDamage.HealBurn(bodyPart, burnHeal * ent.Comp.LimbHealShare);

        DwarfLimbDamage.ResetAttemptIfHealed(bodyPart);
        Dirty(part, bodyPart);
    }
}
