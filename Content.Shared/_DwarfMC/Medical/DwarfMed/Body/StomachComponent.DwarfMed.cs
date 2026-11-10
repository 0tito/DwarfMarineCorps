using Content.Shared.Body.Components;

namespace Content.Shared.Body.Components;

public sealed partial class StomachComponent
{
    public float GetDwarfMedIntervalMultiplier()
    {
        return UpdateIntervalMultiplier;
    }

    public void SetDwarfMedIntervalMultiplier(float value)
    {
        UpdateIntervalMultiplier = value;
    }
}
