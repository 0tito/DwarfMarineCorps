using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfFractureEffectSystem : EntitySystem
{
    private const float UseDropChance = 0.2f;
    private const float AttackDropChance = 0.15f;
    private const float MovementDropChance = 0.1f;

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnUseInHand(UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryFractureDrop(args.User, UseDropChance))
            args.Handled = true;
    }

    private void OnAttackAttempt(AttackAttemptEvent args)
    {
        TryFractureDrop(args.Uid, AttackDropChance);
    }

    public bool TryMovementDrop(EntityUid user)
    {
        return TryFractureDrop(user, MovementDropChance);
    }

    public bool TryFractureDrop(EntityUid user, float chance)
    {
        if (!TryComp<DwarfAnatomyComponent>(user, out var anatomy) || !anatomy.Enabled)
            return false;

        if (!TryGetFracturedActiveArm(user, out _))
            return false;

        if (chance < 1f && !_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return false;

        if (!TryComp<HandsComponent>(user, out var hands) ||
            hands.ActiveHandId is not { } handId ||
            !_hands.TryDrop((user, hands), handId))
        {
            return false;
        }

        _popup.PopupEntity("Your fractured arm gives out!", user, user, PopupType.MediumCaution);
        return true;
    }

    private bool TryGetFracturedActiveArm(EntityUid user, out EntityUid part)
    {
        part = default;

        if (!TryComp<HandsComponent>(user, out var hands) ||
            !_hands.TryGetHand((user, hands), hands.ActiveHandId, out var hand) ||
            hand is not { } activeHand)
        {
            return false;
        }

        var symmetry = activeHand.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };

        if (symmetry == BodyPartSymmetry.None ||
            !TryComp<BodyComponent>(user, out var body))
        {
            return false;
        }

        foreach (var (id, bodyPart) in _body.GetBodyChildren(user, body))
        {
            if (bodyPart.PartType is not (BodyPartType.Hand or BodyPartType.Arm) ||
                bodyPart.Symmetry != symmetry)
            {
                continue;
            }

            if (CompOrNull<DwarfBodyPartConditionComponent>(id) is not { Fractured: true, Splinted: false })
                continue;

            part = id;
            return true;
        }

        return false;
    }
}
