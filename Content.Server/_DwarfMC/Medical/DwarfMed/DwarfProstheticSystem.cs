using Content.Server.Humanoid;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfProstheticSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
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
        UpdateDamageOverlayState(ent.Owner, args.Part);

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
        UpdateDamageOverlayState(ent.Owner, args.Part);

        if (!HasComp<DwarfProstheticComponent>(args.Part) ||
            args.Part.Comp.ToHumanoidLayers() is not { } layer)
        {
            return;
        }

        _humanoid.SetBaseLayerColor(ent.Owner, layer, null);
    }

    private void UpdateDamageOverlayState(EntityUid body, Entity<BodyPartComponent> part)
    {
        if (!HasComp<DwarfAnatomyComponent>(body) ||
            !TryComp(body, out BodyComponent? bodyComp) ||
            GetDamageOverlayLayer(part.Comp) is not { } layer)
        {
            return;
        }

        var disabled = layer switch
        {
            HumanoidVisualLayers.LArm => !HasPart(body, bodyComp, BodyPartType.Arm, BodyPartSymmetry.Left) ||
                                         !HasPart(body, bodyComp, BodyPartType.Hand, BodyPartSymmetry.Left),
            HumanoidVisualLayers.RArm => !HasPart(body, bodyComp, BodyPartType.Arm, BodyPartSymmetry.Right) ||
                                         !HasPart(body, bodyComp, BodyPartType.Hand, BodyPartSymmetry.Right),
            HumanoidVisualLayers.LLeg => !HasPart(body, bodyComp, BodyPartType.Leg, BodyPartSymmetry.Left) ||
                                         !HasPart(body, bodyComp, BodyPartType.Foot, BodyPartSymmetry.Left),
            HumanoidVisualLayers.RLeg => !HasPart(body, bodyComp, BodyPartType.Leg, BodyPartSymmetry.Right) ||
                                         !HasPart(body, bodyComp, BodyPartType.Foot, BodyPartSymmetry.Right),
            HumanoidVisualLayers.Head => !HasPart(body, bodyComp, BodyPartType.Head, BodyPartSymmetry.None),
            HumanoidVisualLayers.Chest => !HasPart(body, bodyComp, BodyPartType.Torso, BodyPartSymmetry.None),
            _ => false,
        };

        _appearance.SetData(body, layer, disabled);
    }

    private bool HasPart(
        EntityUid body,
        BodyComponent bodyComp,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        foreach (var (_, part) in _body.GetBodyChildren(body, bodyComp))
        {
            if (part.PartType != type)
                continue;

            if (symmetry != BodyPartSymmetry.None && part.Symmetry != symmetry)
                continue;

            return true;
        }

        return false;
    }

    private static HumanoidVisualLayers? GetDamageOverlayLayer(BodyPartComponent part)
    {
        // Hands and feet do not have their own damage overlay sprites. Their blood is
        // drawn by the parent arm/leg overlay, so update that overlay when they change.
        return part.PartType switch
        {
            BodyPartType.Hand => part.Symmetry switch
            {
                BodyPartSymmetry.Left => HumanoidVisualLayers.LArm,
                BodyPartSymmetry.Right => HumanoidVisualLayers.RArm,
                _ => null,
            },
            BodyPartType.Foot => part.Symmetry switch
            {
                BodyPartSymmetry.Left => HumanoidVisualLayers.LLeg,
                BodyPartSymmetry.Right => HumanoidVisualLayers.RLeg,
                _ => null,
            },
            _ => part.ToHumanoidLayers(),
        };
    }
}
