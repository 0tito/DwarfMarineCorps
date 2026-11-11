using Content.Shared._RMC14.Medical.Defibrillator;

namespace Content.Shared._RMC14.Medical.Defibrillator;

public sealed partial class RMCDefibrillatorBlockedComponent
{
    public void SetDwarfMedReason(string popup, string examine, bool showOnExamine)
    {
        Popup = popup;
        Examine = examine;
        ShowOnExamine = showOnExamine;
    }
}
