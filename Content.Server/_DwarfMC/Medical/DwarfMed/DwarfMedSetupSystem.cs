using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Damage;
using Robust.Shared.GameObjects;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfMedSetupSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DwarfAnatomyComponent> ent, ref MapInitEvent args)
    {
        RemComp<MaxDamageComponent>(ent.Owner);
    }
}
