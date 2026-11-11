using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfSplintSystem : EntitySystem
{
    [Dependency] private readonly BodyZoneSystem _bodyZone = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfSplintComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfSplintDoAfterEvent>(OnSplintDoAfter);
    }

    private void OnAfterInteract(Entity<DwarfSplintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var user = args.User;
        var target = args.Target ?? user;

        if (!HasComp<DwarfAnatomyComponent>(target) ||
            !TryComp<BodyZoneTargetComponent>(user, out var selected))
        {
            return;
        }

        var zone = selected.SelectedZone;
        if (!TryGetSplintablePart(target, zone, out _))
            return;

        var ev = new DwarfSplintDoAfterEvent(zone);
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.DoAfter, ev, target, target, ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            MovementThreshold = 0.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnSplintDoAfter(Entity<DwarfAnatomyComponent> ent, ref DwarfSplintDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || args.Used is not { } used)
            return;

        if (TryApplySplint(args.User, target, used, args.Zone))
            args.Handled = true;
    }

    public bool TryApplySplint(EntityUid user, EntityUid target, EntityUid splint, BodyZone zone)
    {
        if (!HasComp<DwarfSplintComponent>(splint) ||
            !TryGetSplintablePart(target, zone, out var part) ||
            !TryComp<DwarfBodyPartConditionComponent>(part, out var condition))
        {
            return false;
        }

        condition.Splinted = true;
        Dirty(part, condition);

        if (TryComp<StackComponent>(splint, out var stack))
            _stacks.Use(splint, 1, stack);
        else
            QueueDel(splint);

        _popup.PopupEntity($"You splint the {zone}.", target, user);
        return true;
    }

    private bool TryGetSplintablePart(EntityUid target, BodyZone zone, out EntityUid part)
    {
        if (!_bodyZone.TryGetPartForZone(target, zone, out part) ||
            !HasComp<DwarfBodyPartComponent>(part) ||
            CompOrNull<DwarfBodyPartConditionComponent>(part) is not { Fractured: true, Splinted: false })
        {
            part = default;
            return false;
        }

        return true;
    }


}
