using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Systems;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfBleedingSystem : EntitySystem
{
    private const float IncisionBleedRate = 0.1f;

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
    }
}
