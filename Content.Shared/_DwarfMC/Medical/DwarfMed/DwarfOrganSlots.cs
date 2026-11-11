namespace Content.Shared._DwarfMC.Medical.DwarfMed;

public static class DwarfOrganSlots
{
    public static bool TryGetSlotId(DwarfOrganType type, out string slotId)
    {
        slotId = type.ToString().ToLowerInvariant();
        return true;
    }
}
