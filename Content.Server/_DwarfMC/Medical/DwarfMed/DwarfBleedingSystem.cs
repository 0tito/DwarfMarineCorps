using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Systems;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfBleedingSystem : EntitySystem
{
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
            if (TryComp<DwarfBodyPartConditionComponent>(part, out var condition) &&
                condition.StumpBleeding)
            {
                args.Amount += condition.StumpBleedRate;
            }
        }
    }
}
