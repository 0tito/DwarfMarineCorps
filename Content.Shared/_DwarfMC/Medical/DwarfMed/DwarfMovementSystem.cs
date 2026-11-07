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

        var legs = _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Leg).Count();
        if (legs < 2)
            return;

        var feet = _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Foot).Count();
        var multiplier = feet switch
        {
            2 => 1f,
            1 => 0.85f,
            _ => 0.7f,
        };

        if (multiplier < 1f)
            args.ModifySpeed(multiplier, multiplier);
    }
}
