// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Goob-Station's _Shitmed PartStatus UI.

using Content.Shared.FixedPoint;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

/// <summary>
///     Visual severity of a DwarfMed body zone. The numeric values match the
///     sprite state suffixes in <c>_DwarfMC/Interface/Medical/PartStatus</c>.
/// </summary>
public enum DwarfPartSeverity : byte
{
    Healthy = 0,
    LightlyWounded = 1,
    SomewhatWounded = 2,
    ModeratelyWounded = 3,
    HeavilyWounded = 4,
    CriticallyWounded = 5,
    Severed = 6,
    Dead = 7,
    Disabled = 8,
}

public static class DwarfPartStatus
{
    public static DwarfPartSeverity GetSeverity(DwarfBodyPartScan scan, bool dead = false)
    {
        if (dead)
            return DwarfPartSeverity.Dead;

        if (scan.Severed)
            return DwarfPartSeverity.Severed;

        var ratio = GetWorstDamageRatio(scan);
        if (ratio <= 0f)
            return DwarfPartSeverity.Healthy;

        if (ratio < 0.2f)
            return DwarfPartSeverity.LightlyWounded;

        if (ratio < 0.4f)
            return DwarfPartSeverity.SomewhatWounded;

        if (ratio < 0.6f)
            return DwarfPartSeverity.ModeratelyWounded;

        if (ratio < 0.8f)
            return DwarfPartSeverity.HeavilyWounded;

        return DwarfPartSeverity.CriticallyWounded;
    }

    public static float GetWorstDamageRatio(DwarfBodyPartScan scan)
    {
        var worst = 0f;

        if (scan.SlashThreshold > FixedPoint2.Zero)
            worst = MathF.Max(worst, scan.SlashDamage.Float() / scan.SlashThreshold.Float());

        if (scan.BruteThreshold > FixedPoint2.Zero)
            worst = MathF.Max(worst, scan.BruteDamage.Float() / scan.BruteThreshold.Float());

        if (scan.BurnThreshold > FixedPoint2.Zero)
            worst = MathF.Max(worst, scan.BurnDamage.Float() / scan.BurnThreshold.Float());

        return worst;
    }

    public static string GetRsiName(BodyZone zone)
    {
        return zone == BodyZone.Chest ? "torso" : zone.ToString().ToLowerInvariant();
    }
}
