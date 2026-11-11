using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    public List<EntityUid> TryDetachPart(EntityUid body, EntityUid partId)
    {
        var detached = new List<EntityUid>();

        if (!TryComp(body, out BodyComponent? bodyComp) ||
            !TryComp(partId, out BodyPartComponent? part))
        {
            return detached;
        }

        if (bodyComp.RootContainer.ContainedEntity == partId)
            return detached;

        if (!TryGetParentBodyPart(partId, out var parentUid, out _))
            return detached;

        if (!TryFindPartSlot(parentUid.Value, partId, out var slotId) ||
            !Containers.TryGetContainer(parentUid.Value, GetPartSlotContainerId(slotId), out var container))
        {
            return detached;
        }

        DetachChildParts(partId, part, detached);
        if (Containers.Remove(partId, container))
            detached.Add(partId);

        return detached;
    }

    private void DetachChildParts(
        EntityUid partId,
        BodyPartComponent part,
        List<EntityUid> detached)
    {
        foreach (var (slotId, _) in part.Children)
        {
            if (!Containers.TryGetContainer(partId, GetPartSlotContainerId(slotId), out var container))
                continue;

            foreach (var child in container.ContainedEntities.ToArray())
            {
                if (!TryComp(child, out BodyPartComponent? childPart))
                    continue;

                DetachChildParts(child, childPart, detached);
                if (Containers.Remove(child, container))
                    detached.Add(child);
            }
        }
    }

    private bool TryFindPartSlot(EntityUid parentUid, EntityUid partId, out string slotId)
    {
        slotId = string.Empty;

        if (!TryComp(parentUid, out BodyPartComponent? parentPart))
            return false;

        foreach (var (candidateSlotId, _) in parentPart.Children)
        {
            if (!Containers.TryGetContainer(parentUid, GetPartSlotContainerId(candidateSlotId), out var container))
                continue;

            if (!container.ContainedEntities.Contains(partId))
                continue;

            slotId = candidateSlotId;
            return true;
        }

        return false;
    }
}
