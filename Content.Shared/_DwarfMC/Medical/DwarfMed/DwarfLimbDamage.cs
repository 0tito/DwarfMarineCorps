using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public static class DwarfLimbDamage
{
    public static readonly string[] SlashTypes = ["Slash"];
    public static readonly string[] BruteTypes = ["Blunt", "Piercing"];
    public static readonly string[] AllBruteTypes = ["Slash", "Blunt", "Piercing"];
    public static readonly string[] BurnTypes = ["Heat", "Shock", "Cold", "Caustic"];

    public static FixedPoint2 GetDamage(DamageSpecifier damage, params string[] types)
    {
        var total = FixedPoint2.Zero;
        foreach (var type in types)
        {
            if (damage.DamageDict.TryGetValue(type, out var value) && value > FixedPoint2.Zero)
                total += value;
        }

        return total;
    }

    public static FixedPoint2 GetDamageRaw(DamageSpecifier damage, params string[] types)
    {
        var total = FixedPoint2.Zero;
        foreach (var type in types)
        {
            if (damage.DamageDict.TryGetValue(type, out var value))
                total += value;
        }

        return total;
    }

    public static FixedPoint2 AddCapped(FixedPoint2 current, FixedPoint2 amount, FixedPoint2 cap)
    {
        var next = current + amount;
        return next > cap ? cap : next;
    }

    public static bool HasReachedThreshold(DwarfBodyPartComponent part)
    {
        return part.SlashDamage >= part.SlashThreshold ||
               part.BruteDamage >= part.BruteThreshold ||
               part.BurnDamage >= part.BurnThreshold;
    }

    public static bool IsBurnThresholdReached(DwarfBodyPartComponent part)
    {
        return part.BurnDamage >= part.BurnThreshold;
    }

    public static void HealBrute(DwarfBodyPartComponent part, FixedPoint2 amount)
    {
        var total = part.SlashDamage + part.BruteDamage;
        if (total <= FixedPoint2.Zero || amount <= FixedPoint2.Zero)
            return;

        var ratio = MathF.Min(1f, amount.Float() / total.Float());
        part.SlashDamage = Reduce(part.SlashDamage, part.SlashDamage * ratio);
        part.BruteDamage = Reduce(part.BruteDamage, part.BruteDamage * ratio);
    }

    public static void HealBurn(DwarfBodyPartComponent part, FixedPoint2 amount)
    {
        part.BurnDamage = Reduce(part.BurnDamage, amount);
    }

    public static void ResetAttemptIfHealed(DwarfBodyPartComponent part)
    {
        if (!HasReachedThreshold(part))
            part.DismemberAttempted = false;
    }

    public static FixedPoint2 Reduce(FixedPoint2 current, FixedPoint2 amount)
    {
        return current > amount ? current - amount : FixedPoint2.Zero;
    }
}
