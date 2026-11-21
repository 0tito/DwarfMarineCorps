using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._RMC14.Xenonids.Watch;

public sealed class XenoWatchSystem : SharedXenoWatchSystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly XenoEvolutionSystem _xenoEvolution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly XenoPlasmaSystem _plasma = default!;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<XenoWatchedComponent> _xenoWatchedQuery;

    private TimeSpan _nextUpdateTime;
    private TimeSpan _updateEvery;

    private readonly Dictionary<ICommonSession, ICChatRecipientData> _recipients = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _xenoWatchedQuery = GetEntityQuery<XenoWatchedComponent>();

        SubscribeLocalEvent<XenoWatchedComponent, ComponentRemove>(OnWatchedRemove);
        SubscribeLocalEvent<XenoWatchedComponent, EntityTerminatingEvent>(OnWatchedRemove);

        SubscribeLocalEvent<XenoWatchingComponent, ComponentRemove>(OnWatchingRemove);
        SubscribeLocalEvent<XenoWatchingComponent, EntityTerminatingEvent>(OnWatchingRemove);
        SubscribeLocalEvent<XenoComponent, WatchInfoUpdateEvent>(UpdateInfo);

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);

        Subs.CVar(_config, RMCCVars.RMCXenoWatchUpdateEverySeconds, v => _updateEvery = TimeSpan.FromSeconds(v), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        if (time < _nextUpdateTime)
            return;

        _nextUpdateTime = time + _updateEvery;

        var query = EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (!comp.Refresh || !_ui.IsUiOpen(uid, XenoWatchUIKey.Key))
                continue;

            var updateEvent = new WatchInfoUpdateEvent();
            RaiseLocalEvent(uid, ref updateEvent);
        }
    }

    private void OnWatchedRemove<T>(Entity<XenoWatchedComponent> ent, ref T args)
    {
        foreach (var watching in ent.Comp.Watching)
        {
            if (TerminatingOrDeleted(watching))
                continue;

            RemCompDeferred<XenoWatchingComponent>(watching);
        }
    }

    private void OnWatchingRemove<T>(Entity<XenoWatchingComponent> ent, ref T args)
    {
        RemoveWatcher(ent);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        var sourceTransform = Transform(ev.Source);
        var sourcePos = _transform.GetWorldPosition(sourceTransform);

        _recipients.Clear();
        foreach (var session in ev.Recipients)
        {
            if (session.Key.AttachedEntity is not { } recipient ||
                !_xenoWatchedQuery.TryComp(recipient, out var watched))
            {
                continue;
            }

            var recipientTransform = Transform(recipient);
            if (sourceTransform.MapID != recipientTransform.MapID)
                continue;

            var recipientPos = _transform.GetWorldPosition(recipientTransform);
            var range = (sourcePos - recipientPos).Length();
            foreach (var watching in watched.Watching)
            {
                if (!_actorQuery.TryComp(watching, out var actor))
                    continue;

                if (!ev.Recipients.ContainsKey(actor.PlayerSession))
                    _recipients.TryAdd(actor.PlayerSession, new ICChatRecipientData(range, false, true));
            }
        }

        foreach (var recipient in _recipients)
        {
            ev.Recipients.TryAdd(recipient.Key, recipient.Value);
        }
    }

    protected override void OnXenoWatchAction(Entity<XenoComponent> ent, ref XenoWatchActionEvent args)
    {
        args.Handled = true;

        if (_hive.GetHive(ent.Owner) is null)
            return;

        if (!HasQueenPopup(ent))
            return;

        _ui.OpenUi(ent.Owner, XenoWatchUIKey.Key, ent);

        if (BuildWatchState(ent) is { } state)
            _ui.SetUiState(ent.Owner, XenoWatchUIKey.Key, state);
    }

    private void UpdateInfo(Entity<XenoComponent> ent, ref WatchInfoUpdateEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, XenoWatchUIKey.Key))
            return;

        if (BuildWatchState(ent) is { } state)
            _ui.SetUiState(ent.Owner, XenoWatchUIKey.Key, state);
    }

    private XenoWatchBuiState? BuildWatchState(Entity<XenoComponent> ent)
    {
        if (_hive.GetHive(ent.Owner) is not { } hive)
            return null;

        var queen = HasComp<XenoAttachedOvipositorComponent>(ent.Owner);
        var canSetLeader = HasComp<HiveLeaderGranterComponent>(ent.Owner);

        var counted = new List<(string Role, int Tier)>();
        var xenos = new List<Xeno>();
        var query = EntityQueryEnumerator<XenoComponent, HiveMemberComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var comp, out var member, out var metaData))
        {
            if (member.Hive != hive.Owner)
                continue;

            if (_mobState.IsDead(uid))
                continue;

            if (comp.CountedInSlots)
                counted.Add((comp.Role.Id, comp.Tier));

            if (uid == ent.Owner)
                continue;

            var leader = HasComp<HiveLeaderComponent>(uid);
            var isQueen = HasComp<XenoEvolutionGranterComponent>(uid);
            var isKing = comp.Role.Id == "RMCXenoKing";

            EntProtoId? strainOf = null;
            if (TryComp<XenoStrainComponent>(uid, out var strainComp))
                strainOf = strainComp.StrainOf;

            FixedPoint2 evo = 0;
            if (TryComp<XenoEvolutionComponent>(uid, out var evoComp))
                evo = evoComp.Points;

            if (!TryComp<DamageableComponent>(uid, out var damageableComp))
                continue;

            FixedPoint2 health = 1;
            if (_threshold.TryGetIncapPercentage(uid, damageableComp.TotalDamage, out var incap))
                health = 1 - incap.Value;

            xenos.Add(new Xeno(
                GetNetEntity(uid),
                Name(uid, metaData),
                metaData.EntityPrototype?.ID,
                health,
                _plasma.GetPlasmaPercentage(uid),
                evo,
                leader,
                strainOf,
                isQueen,
                isKing));
        }

        xenos.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        var burrowed = Math.Min(Math.Sqrt(hive.Comp.BurrowedLarva * hive.Comp.BurrowedLarvaSlotFactor), hive.Comp.BurrowedLarva);
        FixedPoint2 total = burrowed + counted.Count;

        return new XenoWatchBuiState(xenos,
            hive.Comp.BurrowedLarva,
            counted.Count,
            GetTierSlots(hive, 2, total, counted),
            GetTierSlots(hive, 3, total, counted),
            queen,
            canSetLeader);
    }

    private FixedPoint2 GetTierSlots(Entity<HiveComponent> hive, int tier, FixedPoint2 total, List<(string Role, int Tier)> counted)
    {
        if (!_hive.TryGetTierLimit(new Entity<HiveComponent?>(hive.Owner, hive.Comp), tier, out var limit))
            return 0;

        var free = new Dictionary<EntProtoId, int>(hive.Comp.FreeSlots);
        var existing = 0;
        foreach (var (role, existingTier) in counted)
        {
            if (existingTier < tier)
                continue;

            if (free.TryGetValue(role, out var freeCount) && freeCount > 0)
                free[role] = freeCount - 1;
            else
                existing++;
        }

        var slots = limit * total - existing;
        return slots > 0 ? slots : FixedPoint2.Zero;
    }


    public override void Watch(Entity<HiveMemberComponent?, ActorComponent?, EyeComponent?> watcher, Entity<HiveMemberComponent?> toWatch)
    {
        base.Watch(watcher, toWatch);

        if (!HasQueenPopup(watcher))
            return;

        if (watcher.Owner == toWatch.Owner)
            return;

        if (!_hive.FromSameHive((watcher, watcher.Comp1), toWatch))
            return;

        if (!Resolve(watcher, ref watcher.Comp2, false))
            return;

        _eye.SetTarget(watcher, toWatch, watcher);
        _viewSubscriber.AddViewSubscriber(toWatch, watcher.Comp2.PlayerSession);

        RemoveWatcher(watcher);
        EnsureComp<XenoWatchingComponent>(watcher).Watching = toWatch;
        EnsureComp<XenoWatchedComponent>(toWatch).Watching.Add(watcher);

        var ev = new XenoWatchEvent();
        RaiseLocalEvent(watcher, ref ev);
    }

    public override void Unwatch(Entity<EyeComponent?> watcher, ICommonSession player)
    {
        if (!Resolve(watcher, ref watcher.Comp))
            return;

        var oldTarget = watcher.Comp.Target;

        base.Unwatch(watcher, player);

        if (oldTarget != null && oldTarget != watcher.Owner)
            _viewSubscriber.RemoveViewSubscriber(oldTarget.Value, player);

        RemoveWatcher(watcher);
    }

    private void RemoveWatcher(EntityUid toRemove)
    {
        if (!TryComp(toRemove, out XenoWatchingComponent? watching))
            return;

        if (TryComp(watching.Watching, out XenoWatchedComponent? watched))
        {
            watched.Watching.Remove(toRemove);
            if (watched.Watching.Count == 0)
                RemCompDeferred<XenoWatchedComponent>(watching.Watching.Value);
        }

        watching.Watching = null;
        RemCompDeferred<XenoWatchingComponent>(toRemove);
    }

    private bool HasQueenPopup(EntityUid xeno)
    {
        if (_xenoEvolution.HasLiving<XenoEvolutionGranterComponent>(1))
            return true;

        _popup.PopupEntity(Loc.GetString("rmc-no-queen-hivemind-chat"), xeno, xeno, PopupType.MediumCaution);
        return false;
    }
}
