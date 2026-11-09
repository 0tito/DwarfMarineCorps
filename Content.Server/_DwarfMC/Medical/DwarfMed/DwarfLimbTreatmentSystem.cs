using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfLimbTreatmentSystem : EntitySystem
{
    private const float StumpPackRateMultiplier = 0.5f;
    private const float MinStumpBleedRate = 0.05f;

    [Dependency] private readonly BodyZoneSystem _bodyZone = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DwarfAnatomyComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfLimbTreatmentDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfStumpPackingDoAfterEvent>(OnStumpPackingDoAfter);
    }

    private void OnGetVerbs(Entity<DwarfAnatomyComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract ||
            !TryComp<BodyZoneTargetComponent>(args.User, out var target) ||
            !_bodyZone.TryGetPartForZone(args.Target, target.SelectedZone, out var part, weights: ent.Comp.HitWeights))
        {
            return;
        }

        var user = args.User;
        var patient = args.Target;
        var zone = target.SelectedZone;

        if (args.Using is { } used &&
            TryComp<WoundTreaterComponent>(used, out var treater) &&
            TryComp<DwarfBodyPartComponent>(part, out var bodyPart))
        {
            var brute = treater.Wound switch
            {
                WoundType.Brute => true,
                WoundType.Burn => false,
                _ => (bool?)null,
            };

            if (brute is { } isBrute)
            {
                var hasDamage = isBrute
                    ? bodyPart.SlashDamage + bodyPart.BruteDamage > FixedPoint2.Zero
                    : bodyPart.BurnDamage > FixedPoint2.Zero;

                if (hasDamage)
                {
                    args.Verbs.Add(new InteractionVerb
                    {
                        Text = $"Treat limb damage on {zone}",
                        Act = () => StartTreatment(user, patient, used, zone, isBrute),
                    });
                }
            }

            if (treater.Wound == WoundType.Brute &&
                CompOrNull<DwarfBodyPartConditionComponent>(part) is { StumpBleeding: true })
            {
                args.Verbs.Add(new InteractionVerb
                {
                    Text = $"Pack {zone} stump",
                    Act = () => StartStumpPacking(user, patient, used, zone),
                });
            }
        }

        if (CompOrNull<DwarfBodyPartConditionComponent>(part) is { Splinted: true } condition)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = $"Remove splint from {zone}",
                Act = () => RemoveSplint(patient, part, condition),
            });
        }
    }

    private void RemoveSplint(EntityUid patient, EntityUid part, DwarfBodyPartConditionComponent condition)
    {
        condition.Splinted = false;
        Dirty(part, condition);
        _popup.PopupEntity("You remove the splint.", patient, PopupType.Medium);
    }

    private void OnInteractUsing(Entity<DwarfAnatomyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<WoundTreaterComponent>(args.Used, out var treater) ||
            !TryComp<BodyZoneTargetComponent>(args.User, out var target) ||
            !_bodyZone.TryGetPartForZone(args.Target, target.SelectedZone, out var part, weights: ent.Comp.HitWeights) ||
            !TryComp<DwarfBodyPartComponent>(part, out var bodyPart))
        {
            return;
        }

        var brute = treater.Wound switch
        {
            WoundType.Brute => true,
            WoundType.Burn => false,
            _ => (bool?)null,
        };

        if (brute is not { } isBrute)
            return;

        var hasDamage = isBrute
            ? bodyPart.SlashDamage + bodyPart.BruteDamage > FixedPoint2.Zero
            : bodyPart.BurnDamage > FixedPoint2.Zero;

        if (!hasDamage)
            return;

        StartTreatment(args.User, args.Target, args.Used, target.SelectedZone, isBrute);
        args.Handled = true;
    }

    private void StartTreatment(EntityUid user, EntityUid target, EntityUid used, BodyZone zone, bool brute)
    {
        var ev = new DwarfLimbTreatmentDoAfterEvent(zone, brute);
        var doAfter = new DoAfterArgs(EntityManager, user, 2, ev, target, target, used)
        {
            BreakOnMove = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            MovementThreshold = 0.5f,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void StartStumpPacking(EntityUid user, EntityUid target, EntityUid used, BodyZone zone)
    {
        var ev = new DwarfStumpPackingDoAfterEvent(zone);
        var doAfter = new DoAfterArgs(EntityManager, user, 8, ev, target, target, used)
        {
            BreakOnMove = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            MovementThreshold = 0.5f,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<DwarfAnatomyComponent> ent, ref DwarfLimbTreatmentDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            args.Target is not { } target ||
            args.Used is not { } used ||
            !TryComp<WoundTreaterComponent>(used, out var treater) ||
            !_bodyZone.TryGetPartForZone(target, args.Zone, out var part, weights: ent.Comp.HitWeights) ||
            !TryComp<DwarfBodyPartComponent>(part, out var bodyPart))
        {
            return;
        }

        args.Handled = true;

        var amount = FixedPoint2.New(10);
        if (treater.Damage is { } damage && damage < FixedPoint2.Zero)
            amount = -damage;

        amount *= ent.Comp.LimbHealShare;

        if (args.Brute)
        {
            DwarfLimbDamage.HealBrute(bodyPart, amount);
        }
        else
        {
            DwarfLimbDamage.HealBurn(bodyPart, amount);
        }

        DwarfLimbDamage.ResetAttemptIfHealed(bodyPart);
        Dirty(part, bodyPart);

        _audio.PlayPvs(treater.TreatEndSound, used);
        ConsumeTreater(used, treater);

        _popup.PopupEntity($"Treated limb damage on {args.Zone}", target, args.User);
    }

    private void OnStumpPackingDoAfter(Entity<DwarfAnatomyComponent> ent, ref DwarfStumpPackingDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            args.Target is not { } target ||
            args.Used is not { } used ||
            !TryComp<WoundTreaterComponent>(used, out var treater) ||
            !_bodyZone.TryGetPartForZone(target, args.Zone, out var part, weights: ent.Comp.HitWeights) ||
            !TryComp<DwarfBodyPartConditionComponent>(part, out var condition) ||
            !condition.StumpBleeding)
        {
            return;
        }

        args.Handled = true;

        TryPackStump(part, condition);

        _audio.PlayPvs(treater.TreatEndSound, used);
        ConsumeTreater(used, treater);

        _popup.PopupEntity($"Packed the {args.Zone} stump; the bleeding slows.", target, args.User);
    }

    public bool TryPackStump(EntityUid part, DwarfBodyPartConditionComponent condition)
    {
        if (!condition.StumpBleeding)
            return false;

        condition.StumpBleedRate = Math.Max(MinStumpBleedRate, condition.StumpBleedRate * StumpPackRateMultiplier);
        Dirty(part, condition);
        return true;
    }

    private void ConsumeTreater(EntityUid used, WoundTreaterComponent treater)
    {
        if (!treater.Consumable)
            return;

        if (TryComp<StackComponent>(used, out var stack))
            _stacks.Use(used, 1, stack);
        else
            QueueDel(used);
    }
}
