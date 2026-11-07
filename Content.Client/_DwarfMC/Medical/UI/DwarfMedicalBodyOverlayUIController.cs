using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._DwarfMC.Medical.UI;

public sealed class DwarfMedicalBodyOverlayUIController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private DwarfMedicalBodyOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_overlay == null)
            return;

        var player = _player.LocalEntity;
        var visible = player is { } entity && EntityManager.HasComponent<BodyZoneTargetComponent>(entity);
        _overlay.Visible = visible;

        if (!visible ||
            !EntityManager.TryGetComponent<BodyZoneTargetComponent>(player!.Value, out var target))
        {
            return;
        }

        _overlay.SetSelected(target.SelectedZone);
    }

    private void OnScreenLoad()
    {
        RemoveOverlay();

        if (UIManager.ActiveScreen?.GetWidget<MainViewport>()?.Parent is not { } parent)
            return;

        _overlay = new DwarfMedicalBodyOverlay();
        _overlay.PartSelected += OnPartSelected;
        parent.AddChild(_overlay);
        LayoutContainer.SetAnchorAndMarginPreset(_overlay, LayoutContainer.LayoutPreset.BottomRight, margin: 5);
    }

    private void OnScreenUnload()
    {
        RemoveOverlay();
    }

    private void OnPartSelected(BodyZone zone)
    {
        EntityManager.EntityNetManager.SendSystemNetworkMessage(new BodyZoneSelectedEvent(zone));
    }

    private void RemoveOverlay()
    {
        if (_overlay != null)
            _overlay.PartSelected -= OnPartSelected;

        _overlay?.Parent?.RemoveChild(_overlay);
        _overlay = null;
    }
}
