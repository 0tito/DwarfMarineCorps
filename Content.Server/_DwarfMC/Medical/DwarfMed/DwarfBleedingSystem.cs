using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Systems;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfBleedingSystem : EntitySystem
{
    private const float IncisionBleedRate = 0.1f;
    private const float HeartBleedRate = 0.25f;
    private const float HeartBleedThreshold = 0.3f;

    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfBleedingEvent>(OnBleeding);
    }

    private void OnBleeding(Entity<DwarfAnatomyComponent> ent, ref DwarfBleedingEvent args)
    {
        foreach (var (part, _) in _body.GetBodyChildren(ent.Owner))
        {
            if (HasComp<CMIncisionOpenComponent>(part))
                args.Amount += IncisionBleedRate;

            if (!TryComp<DwarfBodyPartConditionComponent>(part, out var condition))
                continue;

            if (condition.StumpBleeding)
                args.Amount += condition.StumpBleedRate;

            if (condition.InternalBleeding)
                args.Amount += condition.InternalBleedRate;
        }

        foreach (var (organId, _) in _body.GetBodyOrgans(ent.Owner))
        {
            if (!TryComp<DwarfOrganHealthComponent>(organId, out var organ) ||
                organ.OrganType != DwarfOrganType.Heart ||
                organ.MaxDamage.Float() <= 0f)
            {
                continue;
            }

            var ratio = organ.Damage.Float() / organ.MaxDamage.Float();
            if (ratio <= HeartBleedThreshold)
                continue;

            args.Amount += HeartBleedRate * ratio * (organ.Ruptured ? 2f : 1f);
        }
    }
}
