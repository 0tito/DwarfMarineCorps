using Content.Shared._DwarfMC.Medical.DwarfMed;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfDelimbPolicySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfDelimbAttemptEvent>(OnAttempt);
    }

    private void OnAttempt(Entity<DwarfAnatomyComponent> ent, ref DwarfDelimbAttemptEvent args)
    {
        if (args.Cancelled ||
            !ent.Comp.AllowDelimbing ||
            args.Reason != DwarfSeverReason.Damage &&
            args.Reason != DwarfSeverReason.Fire)
        {
            return;
        }

        if (!TryComp<DwarfBodyPartComponent>(args.Part, out var part) ||
            !part.Dismemberable)
        {
            return;
        }

        if (!DwarfLimbDamage.HasReachedThreshold(part))
            return;

        args.Chance = 1;
        args.Forced = true;
    }
}
