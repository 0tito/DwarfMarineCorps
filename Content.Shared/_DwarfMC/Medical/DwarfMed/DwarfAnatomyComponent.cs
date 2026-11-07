using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DwarfAnatomyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public float LimbDamageShare = 0.5f;

    [DataField]
    public float LimbHealShare = 1.0f;

    [DataField]
    public bool AllowDelimbing = true;

    [DataField]
    public Dictionary<BodyPartType, float> HitWeights = new()
    {
        [BodyPartType.Head] = 10f,
        [BodyPartType.Torso] = 30f,
        [BodyPartType.Groin] = 10f,
        [BodyPartType.Arm] = 16f,
        [BodyPartType.Hand] = 8f,
        [BodyPartType.Leg] = 20f,
        [BodyPartType.Foot] = 8f,
    };
}
