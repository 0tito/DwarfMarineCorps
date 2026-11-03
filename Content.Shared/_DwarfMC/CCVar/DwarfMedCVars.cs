using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._DwarfMC.CCVar;

[CVarDefs]
public sealed partial class DwarfMedCVars : CVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("dmc.dwarfmed.enabled", true, CVar.REPLICATED | CVar.SERVER);
}
