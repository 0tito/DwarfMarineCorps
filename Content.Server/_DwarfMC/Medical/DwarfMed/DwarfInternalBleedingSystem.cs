using Content.Shared._DwarfMC.CCVar;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfInternalBleedingSystem : EntitySystem
{
    private const float MovementBleedChance = 0.15f;
    private const float BruteBaseChance = 0.2f;

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly DwarfFractureEffectSystem _fractureEffects = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextMovementCheck = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<DwarfAnatomyComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<DwarfAnatomyComponent> ent, ref ComponentShutdown args)
    {
        _nextMovementCheck.Remove(ent.Owner);
    }

    private void OnMove(Entity<DwarfAnatomyComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        var now = _timing.CurTime;
        if (_nextMovementCheck.TryGetValue(ent.Owner, out var next) && now < next)
            return;

        _nextMovementCheck[ent.Owner] = now + TimeSpan.FromSeconds(1);
        CheckMovement(ent);
        _fractureEffects.TryMovementDrop(ent.Owner);
    }

    public void CheckMovement(Entity<DwarfAnatomyComponent> ent)
    {
        if (!ent.Comp.Enabled)
            return;

        var chanceMultiplier = _config.GetCVar(DwarfMedCVars.InternalBleedChanceMultiplier);

        foreach (var (partId, _) in _body.GetBodyChildren(ent.Owner))
        {
            if (!TryComp<DwarfBodyPartComponent>(partId, out var part))
                continue;

            if (CompOrNull<DwarfBodyPartConditionComponent>(partId) is not
                { Fractured: true, Splinted: false, InternalBleeding: false })
            {
                continue;
            }

            var chance = MovementBleedChance * part.InternalBleedChanceMultiplier * chanceMultiplier;
            if (_random.Prob(Math.Clamp(chance, 0f, 1f)))
                TryCauseInternalBleeding(ent, partId, part, 1f);
        }
    }

    public bool TryCauseInternalBleedingFromBrute(
        Entity<DwarfAnatomyComponent> ent,
        EntityUid partId,
        DwarfBodyPartComponent part,
        FixedPoint2 bruteDamage)
    {
        if (bruteDamage <= FixedPoint2.Zero)
            return false;

        if (!TryComp<BodyPartComponent>(partId, out var bodyPart) ||
            bodyPart.PartType is not (BodyPartType.Head or BodyPartType.Torso or BodyPartType.Groin))
        {
            return false;
        }

        var threshold = part.BruteThreshold.Float() * 0.5f;
        if (threshold <= 0f || part.BruteDamage.Float() < threshold)
            return false;

        var severity = Math.Clamp((part.BruteDamage.Float() - threshold) / threshold, 0f, 1f);
        var chanceMultiplier = _config.GetCVar(DwarfMedCVars.InternalBleedChanceMultiplier);
        var chance = (BruteBaseChance + (1f - BruteBaseChance) * severity) *
                     part.InternalBleedChanceMultiplier *
                     chanceMultiplier;

        return TryCauseInternalBleeding(ent, partId, part, Math.Clamp(chance, 0f, 1f));
    }

    public bool TryCauseInternalBleeding(
        Entity<DwarfAnatomyComponent> ent,
        EntityUid partId,
        DwarfBodyPartComponent part,
        float chance)
    {
        if (!ent.Comp.Enabled)
            return false;

        var condition = EnsureComp<DwarfBodyPartConditionComponent>(partId);
        if (condition.InternalBleeding)
            return false;

        if (chance < 1f && !_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return false;

        condition.InternalBleeding = true;
        condition.InternalBleedRate = part.InternalBleedRate *
                                      _config.GetCVar(DwarfMedCVars.InternalBleedRateMultiplier);
        Dirty(partId, condition);

        EnsureComp<WoundedComponent>(ent.Owner);

        _popup.PopupEntity("You feel a deep, wet pain inside.", ent.Owner, ent.Owner, PopupType.MediumCaution);
        return true;
    }
}
