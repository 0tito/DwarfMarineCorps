using Content.Shared._DwarfMC.CCVar;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfOrganDamageSystem : EntitySystem
{
    private const float ToxinOrganShare = 0.1f;
    private static readonly TimeSpan ToxinDamageCooldown = TimeSpan.FromSeconds(2);

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

    }

    public void TryDamageFromToxin(Entity<DwarfAnatomyComponent> ent, DamageChangedEvent args)
    {
        if (!ent.Comp.Enabled || args.DamageDelta == null || !args.DamageIncreased)
            return;

        var toxin = DwarfLimbDamage.GetDamage(args.DamageDelta, "Poison", "Radiation");
        if (toxin <= FixedPoint2.Zero)
            return;

        var now = _timing.CurTime;
        var state = EnsureComp<DwarfOrganEffectStateComponent>(ent.Owner);
        if (now < state.NextToxinDamage)
            return;

        state.NextToxinDamage = now + ToxinDamageCooldown;

        var amount = toxin * ToxinOrganShare;
        TryDamageRegionalOrgan(ent.Owner, BodyPartType.Torso, amount);
        TryDamageRegionalOrgan(ent.Owner, BodyPartType.Groin, amount);
    }

    public bool TryDamageOrganFromTrauma(
        Entity<DwarfAnatomyComponent> ent,
        EntityUid partId,
        DwarfBodyPartComponent part,
        FixedPoint2 localTrauma)
    {
        if (!ent.Comp.Enabled || localTrauma <= FixedPoint2.Zero)
            return false;

        var threshold = part.OrganDamageThreshold.Float();
        if (threshold <= 0f || localTrauma.Float() < threshold)
            return false;

        var organs = GetOrgansInPart(partId);
        if (organs.Count == 0)
            return false;

        var impact = localTrauma.Float() / threshold;
        var load = (part.SlashDamage + part.BruteDamage).Float() / threshold;
        var chance = (0.15f + 0.85f * MathF.Min(1f, impact - 1f)) *
                     (1f + load * 0.25f) *
                     part.OrganDamageChanceMultiplier *
                     _config.GetCVar(DwarfMedCVars.OrganDamageChanceMultiplier);
        chance = Math.Clamp(chance, 0f, 1f);

        if (!_random.Prob(chance))
            return false;

        var amount = (localTrauma.Float() - threshold) *
                     ent.Comp.OrganDamageShare *
                     part.OrganDamageRateMultiplier *
                     _config.GetCVar(DwarfMedCVars.OrganDamageRateMultiplier);

        if (amount <= 0f)
            return false;

        var organ = _random.Pick(organs);
        return DamageOrgan(ent.Owner, organ, amount);
    }

    public bool TryDamageRegionalOrgan(EntityUid body, BodyPartType type, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return false;

        foreach (var (partId, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType != type)
                continue;

            return TryDamageOrganInPart(body, partId, amount);
        }

        return false;
    }

    public bool TryDamageOrganInPart(EntityUid body, EntityUid partId, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return false;

        var organs = GetOrgansInPart(partId);
        if (organs.Count == 0)
            return false;

        return DamageOrgan(body, _random.Pick(organs), amount);
    }

    public bool DamageOrgan(EntityUid body, EntityUid organId, FixedPoint2 amount)
    {
        if (!TryComp<DwarfOrganHealthComponent>(organId, out var organ) || amount <= FixedPoint2.Zero)
            return false;

        var wasRuptured = organ.Ruptured;
        var next = organ.Damage + amount;
        organ.Damage = next > organ.MaxDamage ? organ.MaxDamage : next;

        if (!wasRuptured && organ.Damage >= organ.RuptureThreshold)
        {
            organ.Ruptured = true;
            _popup.PopupEntity(
                Loc.GetString("dwarfmed-organ-ruptured", ("organ", organ.OrganType.ToString().ToLowerInvariant())),
                body,
                body,
                PopupType.LargeCaution);
        }

        Dirty(organId, organ);
        return true;
    }

    public List<EntityUid> GetOrgansInPart(EntityUid partId)
    {
        var result = new List<EntityUid>();
        foreach (var (id, _) in _body.GetPartOrgans(partId))
        {
            if (HasComp<DwarfOrganHealthComponent>(id))
                result.Add(id);
        }

        return result;
    }
}
