using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Movement.Systems;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public sealed class DwarfMovementSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<DwarfAnatomyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        var multiplier = 1f;

        var legs = _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Leg).Count();
        if (legs >= 2)
        {
            var feet = _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Foot).Count();
            multiplier *= feet switch
            {
                2 => 1f,
                1 => 0.85f,
                _ => 0.7f,
            };
        }

        var fractureMultiplier = 1f;
        foreach (var (id, part) in _body.GetBodyChildren(ent.Owner))
        {
            if (part.PartType is not (BodyPartType.Leg or BodyPartType.Foot))
                continue;

            if (CompOrNull<DwarfBodyPartConditionComponent>(id) is not { Fractured: true } condition)
                continue;

            var partMultiplier = condition.Splinted ? 0.9f : 0.65f;
            fractureMultiplier = MathF.Min(fractureMultiplier, partMultiplier);
        }

        multiplier *= fractureMultiplier;

        if (multiplier < 1f)
            args.ModifySpeed(multiplier, multiplier);
    }
}
