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
        return GetSeverity(scan, null, dead);
    }

    public static DwarfPartSeverity GetSeverity(
        DwarfBodyPartScan scan,
        IReadOnlyList<DwarfOrganScan>? organs,
        bool dead = false)
    {
        if (dead)
            return DwarfPartSeverity.Dead;

        if (scan.Severed)
            return DwarfPartSeverity.Severed;

        var ratio = GetWorstDamageRatio(scan);
        if (scan.Fractured)
            ratio = MathF.Max(ratio, 0.4f);

        if (organs != null && Enum.TryParse<BodyZone>(scan.SlotId, out var zone))
            ratio = MathF.Max(ratio, GetWorstOrganDamageRatio(zone, organs));

        return GetSeverity(ratio);
    }

    public static DwarfPartSeverity GetSeverity(float ratio)
    {
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

    public static float GetWorstOrganDamageRatio(BodyZone zone, IReadOnlyList<DwarfOrganScan> organs)
    {
        var worst = 0f;

        foreach (var organ in organs)
        {
            if (organ.Zone != zone)
                continue;

            if (organ.Ruptured)
                worst = MathF.Max(worst, 1f);

            if (organ.MaxDamage > FixedPoint2.Zero)
                worst = MathF.Max(worst, organ.Damage.Float() / organ.MaxDamage.Float());
        }

        return worst;
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

    public static string GetDisplayName(BodyZone zone)
    {
        return zone switch
        {
            BodyZone.LeftArm => "Left Arm",
            BodyZone.RightArm => "Right Arm",
            BodyZone.LeftHand => "Left Hand",
            BodyZone.RightHand => "Right Hand",
            BodyZone.LeftLeg => "Left Leg",
            BodyZone.RightLeg => "Right Leg",
            BodyZone.LeftFoot => "Left Foot",
            BodyZone.RightFoot => "Right Foot",
            _ => zone.ToString(),
        };
    }
}
