using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfRejuvenateSystem : EntitySystem
{
    [Dependency] private readonly DwarfAutodocSystem _autodoc = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DwarfSurgerySystem _surgery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<DwarfAnatomyComponent> ent, ref RejuvenateEvent args)
    {
        if (!ent.Comp.Enabled || !TryComp(ent.Owner, out BodyComponent? body))
            return;

        var headRestored = RestoreMissingParts(ent.Owner, body);

        foreach (var (partId, _) in _body.GetBodyChildren(ent.Owner, body))
        {
            if (TryComp<DwarfBodyPartComponent>(partId, out var part))
            {
                part.SlashDamage = FixedPoint2.Zero;
                part.BruteDamage = FixedPoint2.Zero;
                part.BurnDamage = FixedPoint2.Zero;
                part.DismemberAttempted = false;
                Dirty(partId, part);
            }

            RemComp<DwarfBodyPartConditionComponent>(partId);
            _autodoc.CloseDwarfIncision(partId);
        }

        foreach (var (organId, _) in _body.GetBodyOrgans(ent.Owner, body))
        {
            if (!TryComp<DwarfOrganHealthComponent>(organId, out var organ))
                continue;

            _surgery.RepairOrgan(ent.Owner, organId, organ);
        }

        if (headRestored)
        {
            RemComp<DwarfOrganUnrevivableComponent>(ent.Owner);
            RemComp<UnrevivableComponent>(ent.Owner);
        }

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private bool RestoreMissingParts(EntityUid bodyId, BodyComponent body)
    {
        if (body.Prototype is not { } prototypeId ||
            !_prototypes.TryIndex(prototypeId, out var prototype) ||
            !prototype.Slots.TryGetValue(prototype.Root, out var rootSlot) ||
            rootSlot.Part is not { } rootProto)
        {
            return false;
        }

        var root = _body.GetRootPartOrNull(bodyId, body);
        EntityUid rootId;
        if (root is { } existingRoot)
        {
            rootId = existingRoot.Entity;
        }
        else
        {
            rootId = SpawnInContainerOrDrop(rootProto, bodyId, SharedBodySystem.BodyRootContainerId);
            SetupOrgans(rootId, rootSlot.Organs);
        }

        var headRestored = false;
        var parts = new Dictionary<string, EntityUid> { [prototype.Root] = rootId };
        var queue = new Queue<string>();
        queue.Enqueue(prototype.Root);

        while (queue.TryDequeue(out var slotId))
        {
            if (!prototype.Slots.TryGetValue(slotId, out var slot) ||
                !parts.TryGetValue(slotId, out var parentId) ||
                !TryComp(parentId, out BodyPartComponent? parentPart))
            {
                continue;
            }

            foreach (var connection in slot.Connections)
            {
                if (!prototype.Slots.TryGetValue(connection, out var childSlot) ||
                    parts.ContainsKey(connection))
                {
                    continue;
                }

                if (TryGetAttachedChild(parentId, connection, out var existingChild))
                {
                    parts[connection] = existingChild;
                    queue.Enqueue(connection);
                    continue;
                }

                if (childSlot.Part is not { } childProto)
                    continue;

                var childId = Spawn(childProto, Transform(bodyId).Coordinates);
                if (!TryComp(childId, out BodyPartComponent? childPart))
                {
                    QueueDel(childId);
                    continue;
                }

                _body.TryCreatePartSlot(parentId, connection, childPart.PartType, out _, parentPart);

                if (!_body.AttachPart(parentId, connection, childId, parentPart, childPart))
                {
                    QueueDel(childId);
                    continue;
                }

                SetupOrgans(childId, childSlot.Organs);
                parts[connection] = childId;
                queue.Enqueue(connection);

                if (childPart.PartType == BodyPartType.Head)
                    headRestored = true;
            }
        }

        return headRestored;
    }

    private bool TryGetAttachedChild(EntityUid parentId, string slotId, out EntityUid child)
    {
        child = default;

        if (!_container.TryGetContainer(parentId, SharedBodySystem.GetPartSlotContainerId(slotId), out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return false;
        }

        child = container.ContainedEntities[0];
        return HasComp<BodyPartComponent>(child);
    }

    private void SetupOrgans(EntityUid partId, Dictionary<string, string> organs)
    {
        foreach (var (slotId, organProto) in organs)
        {
            if (!_body.TryCreateOrganSlot(partId, slotId, out _))
                continue;

            SpawnInContainerOrDrop(organProto, partId, SharedBodySystem.GetOrganContainerId(slotId));
        }
    }
}
