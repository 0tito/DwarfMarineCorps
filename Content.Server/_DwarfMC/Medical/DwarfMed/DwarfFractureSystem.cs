using Content.Shared._DwarfMC.CCVar;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfFractureSystem : EntitySystem
{
    private static readonly SoundPathSpecifier FractureSound = new("/Audio/Effects/bone_rattle.ogg");

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public float ComputeFractureChance(DwarfBodyPartComponent part, FixedPoint2 localTrauma)
    {
        if (localTrauma <= FixedPoint2.Zero)
            return 0f;

        var scale = part.FractureScale.Float();
        if (scale <= 0f)
            scale = 100f;

        var impact = localTrauma.Float() / scale;
        var load = (part.SlashDamage + part.BruteDamage).Float() / scale;
        var chance = (impact * impact + impact * load) * part.FractureChanceMultiplier *
                     _config.GetCVar(DwarfMedCVars.FractureChanceMultiplier);

        return Math.Clamp(chance, 0f, 1f);
    }

    public bool TryFracture(
        Entity<DwarfAnatomyComponent> ent,
        EntityUid partId,
        DwarfBodyPartComponent part,
        FixedPoint2 localTrauma,
        float? chanceOverride = null)
    {
        if (!ent.Comp.Enabled ||
            localTrauma <= FixedPoint2.Zero ||
            HasComp<DwarfProstheticComponent>(partId))
        {
            return false;
        }

        var condition = EnsureComp<DwarfBodyPartConditionComponent>(partId);
        if (condition.Fractured)
            return false;

        var chance = chanceOverride ?? ComputeFractureChance(part, localTrauma);
        if (chance <= 0f || !_random.Prob(chance))
            return false;

        condition.Fractured = true;
        Dirty(partId, condition);

        _audio.PlayPvs(FractureSound, ent.Owner);
        _popup.PopupEntity("You feel a bone crack!", ent.Owner, ent.Owner, PopupType.MediumCaution);

        return true;
    }
}
