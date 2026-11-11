using Content.Server.Humanoid;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfProstheticSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
    }

    private void OnBodyPartAdded(Entity<BodyComponent> ent, ref BodyPartAddedEvent args)
    {
        if (!TryComp<DwarfProstheticComponent>(args.Part, out var prosthetic))
            return;

        EnsureProstheticChildSlots(args.Part);

        if (args.Part.Comp.ToHumanoidLayers() is { } layer)
            _humanoid.SetBaseLayerColor(ent.Owner, layer, prosthetic.LimbColor);
    }

    private void EnsureProstheticChildSlots(Entity<BodyPartComponent> part)
    {
        if (part.Comp.Symmetry == BodyPartSymmetry.None)
            return;

        var left = part.Comp.Symmetry == BodyPartSymmetry.Left;
        switch (part.Comp.PartType)
        {
            case BodyPartType.Arm:
                _body.TryCreatePartSlot(
                    part.Owner,
                    left ? "left_hand" : "right_hand",
                    BodyPartType.Hand,
                    out _);
                break;
            case BodyPartType.Leg:
                _body.TryCreatePartSlot(
                    part.Owner,
                    left ? "left_foot" : "right_foot",
                    BodyPartType.Foot,
                    out _);
                break;
        }
    }

    private void OnBodyPartRemoved(Entity<BodyComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (!HasComp<DwarfProstheticComponent>(args.Part) ||
            args.Part.Comp.ToHumanoidLayers() is not { } layer)
        {
            return;
        }

        _humanoid.SetBaseLayerColor(ent.Owner, layer, null);
    }
}
