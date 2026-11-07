using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public enum BodyZone : byte
{
    Head,
    Chest,
    Groin,
    LeftArm,
    RightArm,
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot,
}
