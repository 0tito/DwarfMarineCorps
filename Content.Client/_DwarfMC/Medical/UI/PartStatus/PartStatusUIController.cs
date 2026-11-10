// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Goob-Station's _Shitmed PartStatus UI.

using Content.Client._DwarfMC.Medical.UI.PartStatus.Widgets;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._DwarfMC.Medical.UI.PartStatus;

public sealed class PartStatusUIController : UIController
{
    private const float UpdateInterval = 0.1f;

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<BodyZone, DwarfPartSeverity> _status = new();
    private DwarfBodyPartScanSystem? _scans;
    private SpriteSystem? _spriteSystem;
    private PartStatusControl? _lastControl;
    private EntityUid? _lastPlayer;
    private TimeSpan _nextUpdate;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var control = UIManager.GetActiveUIWidgetOrNull<PartStatusControl>();
        if (control == null)
        {
            _lastControl = null;
            return;
        }

        if (_lastControl != control)
        {
            _lastControl = control;
            _status.Clear();
            _nextUpdate = TimeSpan.Zero;
        }

        if (_player.LocalEntity is not { } player ||
            !EntityManager.HasComponent<DwarfAnatomyComponent>(player))
        {
            control.SetVisible(false);
            _lastPlayer = null;
            _status.Clear();
            return;
        }

        control.SetVisible(true);

        if (_lastPlayer != player)
        {
            _lastPlayer = player;
            _status.Clear();
            _nextUpdate = TimeSpan.Zero;
        }

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);

        _scans ??= EntityManager.System<DwarfBodyPartScanSystem>();
        var dead = EntityManager.TryGetComponent<MobStateComponent>(player, out var mobState) &&
                   mobState.CurrentState == MobState.Dead;
        var scans = _scans.Build(player);
        var changed = false;
        foreach (var scan in scans)
        {
            if (!Enum.TryParse<BodyZone>(scan.SlotId, out var zone))
                continue;

            var severity = DwarfPartStatus.GetSeverity(scan, dead);
            if (_status.TryGetValue(zone, out var previous) && previous == severity)
                continue;

            _status[zone] = severity;
            changed = true;
        }

        if (changed)
            control.SetTextures(_status);
    }

    public void RequestPartStatus()
    {
        if (!_timing.IsFirstTimePredicted ||
            _player.LocalEntity is not { } player ||
            !EntityManager.HasComponent<DwarfAnatomyComponent>(player))
        {
            return;
        }

        EntityManager.EntityNetManager?.SendSystemNetworkMessage(
            new DwarfPartStatusRequestEvent(EntityManager.GetNetEntity(player)));
    }

    public Texture GetTexture(SpriteSpecifier specifier)
    {
        _spriteSystem ??= EntityManager.System<SpriteSystem>();
        return _spriteSystem.Frame0(specifier);
    }
}
