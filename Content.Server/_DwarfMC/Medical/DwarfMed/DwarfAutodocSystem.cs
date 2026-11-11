using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;
using Content.Shared._RMC14.Medical.Autodoc;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfAutodocSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DwarfSurgerySystem _surgery = default!;

    public bool HasLimbDamage(EntityUid body, bool brute)
    {
        if (!HasComp<DwarfAnatomyComponent>(body))
            return false;

        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<DwarfBodyPartComponent>(partId, out var part))
                continue;

            var damage = brute
                ? part.SlashDamage + part.BruteDamage
                : part.BurnDamage;

            if (damage > FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    public void HealLimbAccumulators(EntityUid body, bool brute, FixedPoint2 amount)
    {
        if (!TryComp<DwarfAnatomyComponent>(body, out var anatomy) || amount <= FixedPoint2.Zero)
            return;

        var heal = amount * anatomy.LimbHealShare;
        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<DwarfBodyPartComponent>(partId, out var part))
                continue;

            if (brute)
                DwarfLimbDamage.HealBrute(part, heal);
            else
                DwarfLimbDamage.HealBurn(part, heal);

            DwarfLimbDamage.ResetAttemptIfHealed(part);
            Dirty(partId, part);
        }
    }

    public void CloseDwarfIncision(EntityUid part)
    {
        RemComp<CMIncisionOpenComponent>(part);
        RemComp<DwarfBoneGelledComponent>(part);
        RemComp<DwarfProstheticInstalledComponent>(part);
        RemComp<DwarfLimbReattachedComponent>(part);
        RemComp<DwarfInternalBleedingRepairedComponent>(part);
        RemComp<DwarfStumpSealedComponent>(part);
        RemComp<DwarfOrganRepairedComponent>(part);
        RemComp<DwarfOrganRemovedComponent>(part);
        RemComp<DwarfOrganInsertedComponent>(part);
    }

    public bool TryCompleteDwarfProcedure(EntityUid autodoc, EntityUid occupant, AutodocSurgeryType type)
    {
        if (!HasComp<DwarfAnatomyComponent>(occupant))
            return false;

        var changed = false;
        switch (type)
        {
            case AutodocSurgeryType.InternalBleeding:
                foreach (var (partId, _) in _body.GetBodyChildren(occupant))
                {
                    if (!TryComp<DwarfBodyPartConditionComponent>(partId, out var condition) ||
                        !condition.InternalBleeding)
                    {
                        continue;
                    }

                    condition.InternalBleeding = false;
                    condition.InternalBleedRate = 0;
                    Dirty(partId, condition);
                    changed = true;
                }

                break;
            case AutodocSurgeryType.BrokenBone:
                foreach (var (partId, _) in _body.GetBodyChildren(occupant))
                {
                    if (!TryComp<DwarfBodyPartConditionComponent>(partId, out var condition) ||
                        (!condition.Fractured && !condition.Splinted))
                    {
                        continue;
                    }

                    condition.Fractured = false;
                    condition.Splinted = false;
                    Dirty(partId, condition);
                    changed = true;
                }

                break;
            case AutodocSurgeryType.OrganDamage:
                foreach (var (organId, _) in _body.GetBodyOrgans(occupant))
                {
                    if (!TryComp<DwarfOrganHealthComponent>(organId, out var organ) ||
                        (organ.Damage <= FixedPoint2.Zero && !organ.Ruptured))
                    {
                        continue;
                    }

                    _surgery.RepairOrgan(occupant, organId, organ);
                    changed = true;
                }

                break;
        }

        return changed;
    }
}
