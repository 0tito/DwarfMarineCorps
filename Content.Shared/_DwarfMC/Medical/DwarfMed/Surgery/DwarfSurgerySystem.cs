using System.Linq;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;

public sealed class DwarfSurgerySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

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
        SubscribeLocalEvent<DwarfSurgeryOrganDamageConditionComponent, CMSurgeryValidEvent>(OnOrganDamageValid);

        SubscribeLocalEvent<DwarfSurgeryStepRepairOrgansEffectComponent, CMSurgeryStepEvent>(OnRepairOrgans);
        SubscribeLocalEvent<DwarfSurgeryStepRepairOrgansEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnRepairOrgansCheck);
        SubscribeLocalEvent<DwarfSurgeryStepBoneGelEffectComponent, CMSurgeryStepEvent>(OnApplyBoneGel);
        SubscribeLocalEvent<DwarfSurgeryStepBoneGelEffectComponent, CMSurgeryStepCompleteCheckEvent>(OnApplyBoneGelCheck);

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

    private void OnOrganDamageValid(Entity<DwarfSurgeryOrganDamageConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<DwarfAnatomyComponent>(args.Body) ||
            (!HasComp<CMIncisionOpenComponent>(args.Part) && GetDamagedOrgans(args.Part, ent.Comp.OrganType).Count == 0))
        {
            args.Cancelled = true;
        }
    }

    private void OnApplyBoneGel(Entity<DwarfSurgeryStepBoneGelEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer || !HasComp<DwarfAnatomyComponent>(args.Body))
            return;

        if (!TryGetGelInserter(args.Tools, out var tool, out var inserter, out var solutionEnt, out var solution))
            return;

        if (IsExactGel(solution, DwarfOrganGels.Bone, inserter.ReagentPerUse))
        {
            _solution.RemoveReagent(solutionEnt, DwarfOrganGels.Bone.Id, inserter.ReagentPerUse);
            EnsureComp<DwarfBoneGelledComponent>(args.Part);
            _popup.PopupEntity(Loc.GetString("dwarfmed-bone-gel-applied"), args.Body, args.User, PopupType.Medium);
            return;
        }

        if (TryConsumeWrongGel(solutionEnt, solution, inserter.ReagentPerUse))
        {
            _popup.PopupEntity(Loc.GetString("dwarfmed-bone-gel-wrong"), args.Body, args.User, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dwarfmed-bone-gel-needed"), args.Body, args.User, PopupType.MediumCaution);
    }

    private void OnApplyBoneGelCheck(Entity<DwarfSurgeryStepBoneGelEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<DwarfBoneGelledComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnRepairOrgans(Entity<DwarfSurgeryStepRepairOrgansEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!_net.IsServer || !HasComp<DwarfAnatomyComponent>(args.Body))
            return;

        if (!TryGetGelInserter(args.Tools, out var tool, out var inserter, out var solutionEnt, out var solution))
            return;

        var damaged = GetDamagedOrgans(args.Part, ent.Comp.OrganType);
        if (damaged.Count == 0)
            return;

        foreach (var (organId, organ) in damaged)
        {
            if (!DwarfOrganGels.TryGet(organ.OrganType, out var required) ||
                !IsExactGel(solution, required, inserter.ReagentPerUse))
            {
                continue;
            }

            _solution.RemoveReagent(solutionEnt, required.Id, inserter.ReagentPerUse);
            RepairOrgan(args.Body, organId, organ);
            _popup.PopupEntity(
                Loc.GetString("dwarfmed-organ-gel-repaired", ("organ", _reagents.Index(required).LocalizedName)),
                args.Body,
                args.User,
                PopupType.Medium);
            return;
        }

        if (TryConsumeWrongGel(solutionEnt, solution, inserter.ReagentPerUse))
        {
            var target = damaged[0];
            target.Comp.Damage = FixedPoint2.Min(target.Comp.MaxDamage, target.Comp.Damage + FixedPoint2.New(inserter.WrongOrganDamage));
            if (target.Comp.Damage >= target.Comp.RuptureThreshold)
                target.Comp.Ruptured = true;
            Dirty(target.Id, target.Comp);

            var toxin = new DamageSpecifier();
            toxin.DamageDict.Add("Poison", FixedPoint2.New(inserter.WrongToxinDamage));
            _damageable.TryChangeDamage(args.Body, toxin, ignoreResistances: true, interruptsDoAfters: false);

            _popup.PopupEntity(Loc.GetString("dwarfmed-organ-gel-wrong"), args.Body, args.User, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dwarfmed-organ-gel-needed"), args.Body, args.User, PopupType.MediumCaution);
    }

    private void OnRepairOrgansCheck(Entity<DwarfSurgeryStepRepairOrgansEffectComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (GetDamagedOrgans(args.Part, ent.Comp.OrganType).Count > 0)
            args.Cancelled = true;
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

    private List<(EntityUid Id, DwarfOrganHealthComponent Comp)> GetDamagedOrgans(
        EntityUid partId,
        DwarfOrganType? requiredType = null)
    {
        var result = new List<(EntityUid, DwarfOrganHealthComponent)>();
        foreach (var (id, _) in _body.GetPartOrgans(partId))
        {
            if (!TryComp<DwarfOrganHealthComponent>(id, out var organ) ||
                (requiredType != null && organ.OrganType != requiredType) ||
                (organ.Damage <= FixedPoint2.Zero && !organ.Ruptured))
            {
                continue;
            }

            result.Add((id, organ));
        }

        return result;
    }

    private bool TryGetGelInserter(
        List<EntityUid> tools,
        out EntityUid tool,
        out DwarfGelInserterComponent inserter,
        out Entity<SolutionComponent> solutionEnt,
        out Solution solution)
    {
        foreach (var held in tools)
        {
            if (!TryComp<DwarfGelInserterComponent>(held, out var comp))
                continue;

            if (!_solution.TryGetSolution(held, comp.SolutionName, out var solEnt, out var sol) ||
                solEnt is not { } solutionEntity)
            {
                continue;
            }

            tool = held;
            inserter = comp;
            solutionEnt = solutionEntity;
            solution = sol;
            return true;
        }

        tool = default;
        inserter = default!;
        solutionEnt = default;
        solution = default!;
        return false;
    }

    private static bool IsExactGel(Solution solution, ProtoId<ReagentPrototype> required, FixedPoint2 amount)
    {
        if (solution.Contents.Count != 1)
            return false;

        var content = solution.Contents[0];
        return content.Reagent.Prototype == required.Id && content.Quantity >= amount;
    }

    private bool TryConsumeWrongGel(
        Entity<SolutionComponent> solutionEnt,
        Solution solution,
        FixedPoint2 amount)
    {
        var candidates = solution.Contents
            .Where(c => DwarfOrganGels.IsGel(c.Reagent.Prototype))
            .OrderByDescending(c => c.Quantity.Float())
            .ToList();

        var total = FixedPoint2.Zero;
        foreach (var candidate in candidates)
            total += candidate.Quantity;

        if (total < amount)
            return false;

        var remaining = amount;
        foreach (var candidate in candidates)
        {
            var take = FixedPoint2.Min(remaining, candidate.Quantity);
            if (take <= FixedPoint2.Zero)
                continue;

            _solution.RemoveReagent(solutionEnt, candidate.Reagent, take);
            remaining -= take;
            if (remaining <= FixedPoint2.Zero)
                return true;
        }

        return true;
    }

    private void RepairOrgan(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ)
    {
        organ.Damage = FixedPoint2.Zero;
        organ.Ruptured = false;
        Dirty(organId, organ);

        var ev = new DwarfOrganRepairedEvent(body, organId);
        RaiseLocalEvent(body, ref ev, broadcast: true);
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
