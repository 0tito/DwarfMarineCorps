using Content.Server.Body.Components;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Stun;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Traits.Assorted;
using Content.Server.Speech.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfOrganEffectSystem : EntitySystem
{
    private const float AsphyxiationPerDamage = 0.02f;
    private const float ToxinPerDamage = 0.02f;
    private const float MissingKidneyToxinPerTick = 1f;
    private const float MissingLiverToxinPerTick = 1f;

    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DwarfOrganDamageSystem _organDamage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, FixedPoint2> _liverAlcoholRates = new();
    private readonly Dictionary<EntityUid, float> _stomachMultipliers = new();

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, TargetDefibrillatedEvent>(OnDefibrillated);
        SubscribeLocalEvent<DwarfOrganHealthComponent, ComponentShutdown>(OnOrganShutdown);
        SubscribeLocalEvent<DwarfOrganRepairedEvent>(OnOrganRepaired);
    }

    private void OnOrganShutdown(Entity<DwarfOrganHealthComponent> ent, ref ComponentShutdown args)
    {
        _liverAlcoholRates.Remove(ent.Owner);
        _stomachMultipliers.Remove(ent.Owner);
    }

    private void OnDefibrillated(Entity<DwarfAnatomyComponent> ent, ref TargetDefibrillatedEvent args)
    {
        if (!ent.Comp.Enabled || ent.Comp.DefibHeartDamage <= 0f)
            return;

        if (!TryGetOrgan(ent.Owner, DwarfOrganType.Heart, out var heart, out _))
            return;

        _organDamage.DamageOrgan(ent.Owner, heart, ent.Comp.DefibHeartDamage);
    }

    private void OnOrganRepaired(ref DwarfOrganRepairedEvent ev)
    {
        if (!TryComp<DwarfOrganHealthComponent>(ev.Organ, out var organ))
            return;

        switch (organ.OrganType)
        {
            case DwarfOrganType.Heart:
                ClearDefibBlock(ev.Body);
                break;
            case DwarfOrganType.Brain:
                ClearBrainUnrevivable(ev.Body);
                break;
            case DwarfOrganType.Eyes:
                ClearEyeDamage(ev.Body);
                break;
            case DwarfOrganType.Liver:
                RestoreLiverMetabolism(ev.Organ);
                break;
            case DwarfOrganType.Stomach:
                RestoreStomachDigestion(ev.Organ);
                break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DwarfAnatomyComponent>();
        while (query.MoveNext(out var body, out var anatomy))
        {
            if (!anatomy.Enabled)
                continue;

            ProcessBody(body);
        }
    }

    public void ProcessBody(EntityUid body)
    {
        var organs = new Dictionary<DwarfOrganType, (EntityUid Id, DwarfOrganHealthComponent Comp)>();
        foreach (var (id, _) in _body.GetBodyOrgans(body))
        {
            if (TryComp<DwarfOrganHealthComponent>(id, out var organ))
                organs[organ.OrganType] = (id, organ);
        }

        var dead = _mobState.IsDead(body);

        if (organs.TryGetValue(DwarfOrganType.Heart, out var heart))
        {
            UpdateHeart(body, heart.Id, heart.Comp, dead);
        }
        else
        {
            EnsureDefibBlock(body);
            if (!dead)
                ApplyDamage(body, "Asphyxiation", 1f);
        }

        if (organs.TryGetValue(DwarfOrganType.Lungs, out var lungs))
        {
            UpdateLungs(body, lungs.Id, lungs.Comp, dead);
        }
        else if (!dead)
        {
            ApplyDamage(body, "Asphyxiation", 2f);
        }

        if (organs.TryGetValue(DwarfOrganType.Brain, out var brain))
            UpdateBrain(body, brain.Id, brain.Comp, dead);
        else
            EnsureBrainUnrevivable(body);

        if (organs.TryGetValue(DwarfOrganType.Eyes, out var eyes))
            UpdateEyes(body, eyes.Id, eyes.Comp, dead);
        else
            ApplyMissingEyes(body);

        if (organs.TryGetValue(DwarfOrganType.Kidneys, out var kidneys))
        {
            UpdateToxin(body, kidneys.Id, kidneys.Comp, dead);
        }
        else if (!dead)
        {
            ApplyDamage(body, "Poison", MissingKidneyToxinPerTick);
        }

        if (organs.TryGetValue(DwarfOrganType.Liver, out var liver))
        {
            UpdateToxin(body, liver.Id, liver.Comp, dead);
            UpdateLiverMetabolism(liver.Id, liver.Comp);
        }
        else if (!dead)
        {
            ApplyDamage(body, "Poison", MissingLiverToxinPerTick);
        }

        if (organs.TryGetValue(DwarfOrganType.Stomach, out var stomach))
            UpdateStomachDigestion(stomach.Id, stomach.Comp);
    }

    private void UpdateHeart(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ, bool dead)
    {
        if (organ.Ruptured)
            EnsureDefibBlock(body);
        else
            ClearDefibBlock(body);

        if (dead)
            return;

        ApplyDamage(body, "Asphyxiation", GetRatio(organ) * organ.MaxDamage.Float() * AsphyxiationPerDamage + (organ.Ruptured ? 1f : 0f));
    }

    private void UpdateLungs(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ, bool dead)
    {
        if (dead)
            return;

        ApplyDamage(body, "Asphyxiation", GetRatio(organ) * organ.MaxDamage.Float() * AsphyxiationPerDamage + (organ.Ruptured ? 2f : 0f));
    }

    private void UpdateBrain(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ, bool dead)
    {
        if (organ.Ruptured)
        {
            EnsureBrainUnrevivable(body);
            return;
        }

        if (HasComp<DwarfOrganUnrevivableComponent>(body))
            ClearBrainUnrevivable(body);

        if (dead)
            return;

        var ratio = GetRatio(organ);
        if (ratio < 0.2f)
            return;

        _status.TryAddStatusEffect<StutteringAccentComponent>(body, "Stutter", TimeSpan.FromSeconds(1.5), true);

        if (ratio >= 0.4f)
            _status.TryAddStatusEffect<JitteringComponent>(body, "Jitter", TimeSpan.FromSeconds(1.5), true);

        if (ratio >= 0.75f)
            _sizeStun.TryKnockOut(body, TimeSpan.FromSeconds(1.5), true);
    }

    private void UpdateEyes(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ, bool dead)
    {
        var state = EnsureComp<DwarfOrganEffectStateComponent>(body);
        if (!TryComp<BlindableComponent>(body, out var blindable))
            return;

        var target = (int) MathF.Round(blindable.MaxDamage * Math.Clamp(GetRatio(organ) * 1.2f, 0f, 1f));
        if (organ.Ruptured)
            target = blindable.MaxDamage;

        var delta = target - state.AppliedEyeDamage;
        if (delta == 0)
            return;

        _blindable.AdjustEyeDamage(new Entity<BlindableComponent?>(body, blindable), delta);
        state.AppliedEyeDamage = target;
    }

    private void ApplyMissingEyes(EntityUid body)
    {
        var state = EnsureComp<DwarfOrganEffectStateComponent>(body);
        if (!TryComp<BlindableComponent>(body, out var blindable))
            return;

        var delta = blindable.MaxDamage - state.AppliedEyeDamage;
        if (delta == 0)
            return;

        _blindable.AdjustEyeDamage(new Entity<BlindableComponent?>(body, blindable), delta);
        state.AppliedEyeDamage = blindable.MaxDamage;
    }

    private void UpdateToxin(EntityUid body, EntityUid organId, DwarfOrganHealthComponent organ, bool dead)
    {
        if (dead)
            return;

        ApplyDamage(body, "Poison", organ.Damage.Float() * ToxinPerDamage * (organ.Ruptured ? 2f : 1f));
    }

    private void UpdateLiverMetabolism(EntityUid organId, DwarfOrganHealthComponent organ)
    {
        if (!TryComp<MetabolizerComponent>(organId, out var metabolizer) ||
            metabolizer.MetabolismGroups == null)
        {
            return;
        }

        foreach (var group in metabolizer.MetabolismGroups)
        {
            if (group.Id != "Alcohol")
                continue;

            if (!_liverAlcoholRates.TryGetValue(organId, out var baseRate))
            {
                baseRate = group.MetabolismRateModifier;
                _liverAlcoholRates[organId] = baseRate;
            }

            var remaining = Math.Clamp(1f - GetRatio(organ), 0.1f, 1f);
            group.MetabolismRateModifier = baseRate * remaining;
            return;
        }
    }

    private void RestoreLiverMetabolism(EntityUid organId)
    {
        if (!_liverAlcoholRates.Remove(organId, out var baseRate) ||
            !TryComp<MetabolizerComponent>(organId, out var metabolizer) ||
            metabolizer.MetabolismGroups == null)
        {
            return;
        }

        foreach (var group in metabolizer.MetabolismGroups)
        {
            if (group.Id == "Alcohol")
            {
                group.MetabolismRateModifier = baseRate;
                return;
            }
        }
    }

    private void UpdateStomachDigestion(EntityUid organId, DwarfOrganHealthComponent organ)
    {
        if (!TryComp<StomachComponent>(organId, out var stomach))
            return;

        if (!_stomachMultipliers.TryGetValue(organId, out var baseMultiplier))
        {
            baseMultiplier = stomach.GetDwarfMedIntervalMultiplier();
            _stomachMultipliers[organId] = baseMultiplier;
        }

        stomach.SetDwarfMedIntervalMultiplier(baseMultiplier * (1f + GetRatio(organ) * 2f));
    }

    private void RestoreStomachDigestion(EntityUid organId)
    {
        if (!_stomachMultipliers.Remove(organId, out var baseMultiplier) ||
            !TryComp<StomachComponent>(organId, out var stomach))
        {
            return;
        }

        stomach.SetDwarfMedIntervalMultiplier(baseMultiplier);
    }

    private void EnsureDefibBlock(EntityUid body)
    {
        if (HasComp<DwarfOrganDefibBlockComponent>(body))
            return;

        EnsureComp<DwarfOrganDefibBlockComponent>(body);
        var blocked = EnsureComp<RMCDefibrillatorBlockedComponent>(body);
        blocked.SetDwarfMedReason(
            "dwarfmed-defib-blocked-heart",
            "dwarfmed-defib-blocked-heart-examine",
            true);
        Dirty(body, blocked);
    }

    private void ClearDefibBlock(EntityUid body)
    {
        if (!HasComp<DwarfOrganDefibBlockComponent>(body))
            return;

        RemComp<DwarfOrganDefibBlockComponent>(body);
        RemComp<RMCDefibrillatorBlockedComponent>(body);
    }

    private void EnsureBrainUnrevivable(EntityUid body)
    {
        if (HasComp<DwarfOrganUnrevivableComponent>(body))
            return;

        EnsureComp<DwarfOrganUnrevivableComponent>(body);

        var unrevivable = EnsureComp<UnrevivableComponent>(body);
        unrevivable.Analyzable = false;
        unrevivable.Cloneable = false;
        unrevivable.ReasonMessage = "dwarfmed-unrevivable-brain";
        Dirty(body, unrevivable);
    }

    private void ClearBrainUnrevivable(EntityUid body)
    {
        if (!HasComp<DwarfOrganUnrevivableComponent>(body))
            return;

        RemComp<DwarfOrganUnrevivableComponent>(body);
        RemComp<UnrevivableComponent>(body);
    }

    private void ClearEyeDamage(EntityUid body)
    {
        var state = EnsureComp<DwarfOrganEffectStateComponent>(body);
        if (state.AppliedEyeDamage != 0 && TryComp<BlindableComponent>(body, out var blindable))
            _blindable.AdjustEyeDamage(new Entity<BlindableComponent?>(body, blindable), -state.AppliedEyeDamage);

        state.AppliedEyeDamage = 0;
    }

    private void ApplyDamage(EntityUid body, string type, float amount)
    {
        if (amount <= 0f)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add(type, FixedPoint2.New(amount));
        _damageable.TryChangeDamage(body, damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private bool TryGetOrgan(EntityUid body, DwarfOrganType type, out EntityUid organId, out DwarfOrganHealthComponent organ)
    {
        foreach (var (id, _) in _body.GetBodyOrgans(body))
        {
            if (!TryComp<DwarfOrganHealthComponent>(id, out var comp) || comp.OrganType != type)
                continue;

            organId = id;
            organ = comp;
            return true;
        }

        organId = default;
        organ = default!;
        return false;
    }

    private static float GetRatio(DwarfOrganHealthComponent organ)
    {
        return organ.MaxDamage.Float() <= 0f ? 0f : organ.Damage.Float() / organ.MaxDamage.Float();
    }
}
