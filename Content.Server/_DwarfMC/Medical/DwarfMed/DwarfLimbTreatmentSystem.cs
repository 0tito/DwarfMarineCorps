using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfLimbTreatmentSystem : EntitySystem
{
    [Dependency] private readonly BodyZoneSystem _bodyZone = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfLimbTreatmentDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<DwarfAnatomyComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract ||
            args.Using is not { } used ||
            !TryComp<WoundTreaterComponent>(used, out var treater) ||
            !TryComp<BodyZoneTargetComponent>(args.User, out var target))
        {
            return;
        }

        var brute = treater.Wound switch
        {
            WoundType.Brute => true,
            WoundType.Burn => false,
            _ => (bool?) null,
        };

        if (brute is not { } isBrute ||
            !_bodyZone.TryGetPartForZone(args.Target, target.SelectedZone, out var part, weights: ent.Comp.HitWeights) ||
            !TryComp<DwarfBodyPartComponent>(part, out var bodyPart))
        {
            return;
        }

        if (isBrute)
        {
            if (bodyPart.SlashDamage + bodyPart.BruteDamage <= FixedPoint2.Zero)
                return;
        }
        else if (bodyPart.BurnDamage <= FixedPoint2.Zero)
        {
            return;
        }

        var user = args.User;
        var patient = args.Target;
        args.Verbs.Add(new InteractionVerb
        {
            Text = $"Treat {target.SelectedZone}",
            Act = () => StartTreatment(user, patient, used, target.SelectedZone, isBrute),
        });
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

        if (treater.Consumable)
        {
            if (TryComp<StackComponent>(used, out var stack))
                _stacks.Use(used, 1, stack);
            else
                QueueDel(used);
        }

        _popup.PopupEntity($"Treated {args.Zone}", target, args.User);
    }
}
