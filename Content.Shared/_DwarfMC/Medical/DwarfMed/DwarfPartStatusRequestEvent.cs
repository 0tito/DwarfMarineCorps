// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Goob-Station's _Shitmed PartStatus UI.

using Robust.Shared.Serialization;

namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[Serializable, NetSerializable]
public sealed class DwarfPartStatusRequestEvent(NetEntity uid) : EntityEventArgs
{
    public readonly NetEntity Uid = uid;
}
