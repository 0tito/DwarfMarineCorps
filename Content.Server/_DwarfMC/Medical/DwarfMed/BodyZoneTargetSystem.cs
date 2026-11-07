using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class BodyZoneTargetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<BodyZoneSelectedEvent>(OnBodyZoneSelected);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Entity) || !HasComp<HandsComponent>(args.Entity))
            return;

        EnsureComp<BodyZoneTargetComponent>(args.Entity);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (TerminatingOrDeleted(args.Entity))
            return;

        RemComp<BodyZoneTargetComponent>(args.Entity);
    }

    private void OnBodyZoneSelected(BodyZoneSelectedEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        if (!Enum.IsDefined(args.Zone))
            return;

        if (!TryComp<BodyZoneTargetComponent>(player, out var target))
            return;

        target.SelectedZone = args.Zone;
        Dirty(player, target);
    }
}
