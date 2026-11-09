using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

public sealed class DwarfSurgerySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfSurgeryFracturedConditionComponent, CMSurgeryValidEvent>(OnFracturedValid);
        SubscribeLocalEvent<DwarfSurgeryInternalBleedingConditionComponent, CMSurgeryValidEvent>(OnInternalBleedingValid);
        SubscribeLocalEvent<DwarfSurgeryStumpBleedingConditionComponent, CMSurgeryValidEvent>(OnStumpBleedingValid);

        SubscribeLocalEvent<DwarfSurgeryStepRepairFractureEffectComponent, CMSurgeryStepEvent>(OnRepairFracture);
        SubscribeLocalEvent<DwarfSurgeryStepStopInternalBleedingEffectComponent, CMSurgeryStepEvent>(OnStopInternalBleeding);
        SubscribeLocalEvent<DwarfSurgeryStepSealStumpEffectComponent, CMSurgeryStepEvent>(OnSealStump);

        SubscribeLocalEvent<DwarfSurgeryStepRepairFractureEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnRepairFractureCheck);
        SubscribeLocalEvent<DwarfSurgeryStepStopInternalBleedingEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnStopInternalBleedingCheck);
        SubscribeLocalEvent<DwarfSurgeryStepSealStumpEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnSealStumpCheck);
        SubscribeLocalEvent<DwarfSurgeryStepReattachLimbEffectComponent, CMSurgeryStepEvent>(OnReattachLimb);
        SubscribeLocalEvent<DwarfSurgeryStepReattachLimbEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnReattachLimbCheck);

        SubscribeLocalEvent<DwarfSurgeryMissingLimbConditionComponent, CMSurgeryValidEvent>(OnMissingLimbValid);

        SubscribeLocalEvent<CMIncisionOpenComponent, ComponentStartup>(OnIncisionOpened);
    }

    private void OnIncisionOpened(Entity<CMIncisionOpenComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsServer ||
            !TryComp<BodyPartComponent>(ent.Owner, out var part) ||
            part.Body is not { } body)
        {
            return;
        }

        EnsureComp<WoundedComponent>(body);
    }

    private void OnFracturedValid(Entity<DwarfSurgeryFracturedConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<DwarfAnatomyComponent>(args.Body) ||
            (!HasComp<CMIncisionOpenComponent>(args.Part) &&
             CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is not { Fractured: true }))
        {
            args.Cancelled = true;
        }
    }

    private void OnInternalBleedingValid(Entity<DwarfSurgeryInternalBleedingConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<DwarfAnatomyComponent>(args.Body) ||
            (!HasComp<CMIncisionOpenComponent>(args.Part) &&
             CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is not { InternalBleeding: true }))
        {
            args.Cancelled = true;
        }
    }

    private void OnStumpBleedingValid(Entity<DwarfSurgeryStumpBleedingConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<DwarfAnatomyComponent>(args.Body) ||
            (!HasComp<CMIncisionOpenComponent>(args.Part) &&
             CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is not { StumpBleeding: true }))
        {
            args.Cancelled = true;
        }
    }

    private void OnMissingLimbValid(Entity<DwarfSurgeryMissingLimbConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<DwarfAnatomyComponent>(args.Body) ||
            (!HasComp<CMIncisionOpenComponent>(args.Part) &&
             (!TryComp<BodyPartComponent>(args.Part, out var part) || !HasMissingChild(args.Part, part))))
        {
            args.Cancelled = true;
        }
    }

    private void OnRepairFracture(Entity<DwarfSurgeryStepRepairFractureEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer || !HasComp<DwarfAnatomyComponent>(args.Body))
            return;

        var condition = EnsureComp<DwarfBodyPartConditionComponent>(args.Part);
        if (!condition.Fractured)
            return;

        condition.Fractured = false;
        condition.Splinted = false;
        Dirty(args.Part, condition);
    }

    private void OnStopInternalBleeding(Entity<DwarfSurgeryStepStopInternalBleedingEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer || !HasComp<DwarfAnatomyComponent>(args.Body))
            return;

        var condition = EnsureComp<DwarfBodyPartConditionComponent>(args.Part);
        if (!condition.InternalBleeding)
            return;

        condition.InternalBleeding = false;
        condition.InternalBleedRate = 0;
        Dirty(args.Part, condition);
    }

    private void OnSealStump(Entity<DwarfSurgeryStepSealStumpEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer || !HasComp<DwarfAnatomyComponent>(args.Body))
            return;

        var condition = EnsureComp<DwarfBodyPartConditionComponent>(args.Part);
        if (!condition.StumpBleeding)
            return;

        condition.StumpBleeding = false;
        condition.StumpBleedRate = 0;
        Dirty(args.Part, condition);
    }

    private void OnReattachLimb(Entity<DwarfSurgeryStepReattachLimbEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer ||
            !HasComp<DwarfAnatomyComponent>(args.Body) ||
            !TryComp<BodyPartComponent>(args.Part, out var parentPart))
        {
            return;
        }

        foreach (var tool in args.Tools)
        {
            if (!TryComp<DwarfSeveredLimbComponent>(tool, out var severed) ||
                severed.OriginalBody != args.Body ||
                !TryComp<BodyPartComponent>(tool, out var limbPart) ||
                !TryFindMissingSlot(args.Part, parentPart, limbPart, out var slot))
            {
                continue;
            }

            _hands.TryDrop(args.User, tool, checkActionBlocker: false);

            if (!_body.AttachPart(args.Part, slot, tool))
                continue;

            RemComp<DwarfSeveredLimbComponent>(tool);

            if (TryComp<DwarfBodyPartConditionComponent>(args.Part, out var condition) &&
                condition.StumpBleeding)
            {
                condition.StumpBleeding = false;
                condition.StumpBleedRate = 0;
                Dirty(args.Part, condition);
            }

            return;
        }
    }

    private void OnReattachLimbCheck(Entity<DwarfSurgeryStepReattachLimbEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var part) ||
            HasMissingChild(args.Part, part))
        {
            args.Cancelled = true;
        }
    }

    private bool HasMissingChild(EntityUid partId, BodyPartComponent part)
    {
        foreach (var slot in part.Children.Values)
        {
            if (!_container.TryGetContainer(partId, SharedBodySystem.GetPartSlotContainerId(slot.Id), out var container) ||
                container.ContainedEntities.Count > 0)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryFindMissingSlot(
        EntityUid parentId,
        BodyPartComponent parent,
        BodyPartComponent limb,
        out BodyPartSlot slot)
    {
        foreach (var candidate in parent.Children.Values)
        {
            if (candidate.Type != limb.PartType)
                continue;

            if (limb.Symmetry == BodyPartSymmetry.Left && !candidate.Id.StartsWith("left_", StringComparison.Ordinal))
                continue;

            if (limb.Symmetry == BodyPartSymmetry.Right && !candidate.Id.StartsWith("right_", StringComparison.Ordinal))
                continue;

            if (!_container.TryGetContainer(parentId, SharedBodySystem.GetPartSlotContainerId(candidate.Id), out var container) ||
                container.ContainedEntities.Count > 0)
            {
                continue;
            }

            slot = candidate;
            return true;
        }

        slot = default;
        return false;
    }

    private void OnRepairFractureCheck(Entity<DwarfSurgeryStepRepairFractureEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is { Fractured: true })
            args.Cancelled = true;
    }

    private void OnStopInternalBleedingCheck(Entity<DwarfSurgeryStepStopInternalBleedingEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is { InternalBleeding: true })
            args.Cancelled = true;
    }

    private void OnSealStumpCheck(Entity<DwarfSurgeryStepSealStumpEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (CompOrNull<DwarfBodyPartConditionComponent>(args.Part) is { StumpBleeding: true })
            args.Cancelled = true;
    }
}
