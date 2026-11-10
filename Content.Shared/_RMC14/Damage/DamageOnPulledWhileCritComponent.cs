using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Damage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCDamageableSystem))]
public sealed partial class DamageOnPulledWhileCritComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier? Damage;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? PullerWhitelist;

    [DataField, AutoNetworkedField]
    public double Every = 0.1;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public bool Pulled;

    [DataField, AutoNetworkedField]
    public double Accumulator;
}
