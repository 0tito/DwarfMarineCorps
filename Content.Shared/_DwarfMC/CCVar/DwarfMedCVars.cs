using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._DwarfMC.CCVar;

[CVarDefs]
public sealed partial class DwarfMedCVars : CVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("dmc.dwarfmed.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> FractureChanceMultiplier =
        CVarDef.Create("dmc.dwarfmed.fracture_chance_multiplier", 1f, CVar.SERVER);

    public static readonly CVarDef<float> InternalBleedChanceMultiplier =
        CVarDef.Create("dmc.dwarfmed.internal_bleed_chance_multiplier", 1f, CVar.SERVER);

    public static readonly CVarDef<float> InternalBleedRateMultiplier =
        CVarDef.Create("dmc.dwarfmed.internal_bleed_rate_multiplier", 1f, CVar.SERVER);
}
