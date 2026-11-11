using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfSeverSystem : EntitySystem
{
    private static readonly SoundPathSpecifier DelimbSound = new("/Audio/Effects/Fluids/blood1.ogg");
    private static readonly SoundPathSpecifier AshSound = new("/Audio/Effects/lightburn.ogg");

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCUnrevivableSystem _unrevivable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DwarfAnatomyComponent, DwarfSeverRequestEvent>(OnSeverRequest);
    }

    private void OnSeverRequest(Entity<DwarfAnatomyComponent> ent, ref DwarfSeverRequestEvent args)
    {
        if (args.Handled ||
            !ent.Comp.Enabled ||
            !ent.Comp.AllowDelimbing ||
            !TryComp<DwarfBodyPartComponent>(args.Part, out var bodyPart) ||
            !bodyPart.Dismemberable ||
            !TryComp<BodyPartComponent>(args.Part, out var part) ||
            part.Body != ent.Owner)
        {
            return;
        }

        if (!BodyZoneSystem.TryGetZoneForPart(part, out var zone))
            zone = BodyZone.Chest;

        EntityUid? parent = null;
        if (_body.TryGetParentBodyPart(args.Part, out var parentUid, out _))
            parent = parentUid;

        var detached = _body.TryDetachPart(ent.Owner, args.Part);
        if (detached.Count == 0)
            return;

        args.Handled = true;
        var prosthetic = HasComp<DwarfProstheticComponent>(args.Part);

        if (!prosthetic && parent is { } parentPart)
        {
            var condition = EnsureComp<DwarfBodyPartConditionComponent>(parentPart);
            condition.StumpBleeding = true;
            condition.StumpBleedRate += bodyPart.StumpBleedRate;
            Dirty(parentPart, condition);
        }

        foreach (var dropped in detached)
        {
            if (args.Reason == DwarfSeverReason.Fire)
            {
                var coords = Transform(dropped).Coordinates;
                _audio.PlayPvs(AshSound, dropped);
                QueueDel(dropped);
                SpawnAtPosition("Ash", coords);
                continue;
            }

            _transform.DropNextTo(dropped, ent.Owner);
            _physics.ApplyLinearImpulse(dropped, _random.NextAngle().ToVec() * (6f + _random.NextFloat(3f)));
            _transform.SetWorldRotation(dropped, _random.NextAngle());

            var droppedZone = BodyZone.Chest;
            if (TryComp<BodyPartComponent>(dropped, out var droppedPart))
                BodyZoneSystem.TryGetZoneForPart(droppedPart, out droppedZone);

            var severed = EnsureComp<DwarfSeveredLimbComponent>(dropped);
            severed.OriginalBody = ent.Owner;
            severed.Zone = droppedZone;
            Dirty(dropped, severed);
        }

        if (args.Reason != DwarfSeverReason.Fire)
            _audio.PlayPvs(DelimbSound, ent.Owner);

        if (!prosthetic)
            EnsureComp<WoundedComponent>(ent.Owner);

        if (zone == BodyZone.Head)
            _unrevivable.MakeUnrevivable(ent.Owner);

        var popup = args.Reason == DwarfSeverReason.Damage
            ? $"Lost {zone}!"
            : $"Severed {zone}";
        _popup.PopupEntity(popup, ent.Owner, PopupType.LargeCaution);
    }
}
