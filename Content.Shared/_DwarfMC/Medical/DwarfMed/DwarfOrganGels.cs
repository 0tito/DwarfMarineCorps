using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public static class DwarfOrganGels
{
    public static readonly ProtoId<ReagentPrototype> Bone = "DwarfBoneGel";
    public static readonly ProtoId<ReagentPrototype> Heart = "DwarfHeartGel";
    public static readonly ProtoId<ReagentPrototype> Lungs = "DwarfLungGel";
    public static readonly ProtoId<ReagentPrototype> Brain = "DwarfBrainGel";
    public static readonly ProtoId<ReagentPrototype> Eyes = "DwarfEyeGel";
    public static readonly ProtoId<ReagentPrototype> Kidneys = "DwarfKidneyGel";
    public static readonly ProtoId<ReagentPrototype> Liver = "DwarfLiverGel";
    public static readonly ProtoId<ReagentPrototype> Stomach = "DwarfStomachGel";

    public static bool TryGet(DwarfOrganType type, out ProtoId<ReagentPrototype> reagent)
    {
        switch (type)
        {
            case DwarfOrganType.Heart:
                reagent = Heart;
                return true;
            case DwarfOrganType.Lungs:
                reagent = Lungs;
                return true;
            case DwarfOrganType.Brain:
                reagent = Brain;
                return true;
            case DwarfOrganType.Eyes:
                reagent = Eyes;
                return true;
            case DwarfOrganType.Kidneys:
                reagent = Kidneys;
                return true;
            case DwarfOrganType.Liver:
                reagent = Liver;
                return true;
            case DwarfOrganType.Stomach:
                reagent = Stomach;
                return true;
            default:
                reagent = default;
                return false;
        }
    }

    public static bool IsGel(ProtoId<ReagentPrototype> reagent)
    {
        return reagent == Bone ||
               reagent == Heart ||
               reagent == Lungs ||
               reagent == Brain ||
               reagent == Eyes ||
               reagent == Kidneys ||
               reagent == Liver ||
               reagent == Stomach;
    }
}
