// SPDX-License-Identifier: AGPL-3.0-or-later
// Adapted from Goob-Station's _Shitmed PartStatus UI.

using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;

namespace Content.Server._DwarfMC.Medical.DwarfMed;

public sealed class DwarfPartStatusSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DwarfBodyPartScanSystem _scans = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DwarfPartStatusRequestEvent>(OnRequest);
    }

    private void OnRequest(DwarfPartStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Uid, out var playerNullable) ||
            playerNullable is not { } player ||
            args.SenderSession.AttachedEntity != player ||
            !HasComp<DwarfAnatomyComponent>(player))
        {
            return;
        }

        var scans = _scans.Build(player);
        var dead = TryComp<MobStateComponent>(player, out var mobState) &&
                   mobState.CurrentState == MobState.Dead;
        var text = BuildMessage(scans, dead);

        _chat.ChatMessageToOne(
            ChatChannel.Emotes,
            text,
            text,
            EntityUid.Invalid,
            false,
            args.SenderSession.Channel,
            recordReplay: false);
    }

    private string BuildMessage(List<DwarfBodyPartScan> scans, bool dead)
    {
        var byZone = new Dictionary<BodyZone, DwarfBodyPartScan>();
        foreach (var scan in scans)
        {
            if (Enum.TryParse<BodyZone>(scan.SlotId, out var zone))
                byZone[zone] = scan;
        }

        var message = new StringBuilder();
        message.Append(Loc.GetString("dwarfmed-part-status-title"));
        message.Append('\n');

        foreach (var zone in Enum.GetValues<BodyZone>())
        {
            if (!byZone.TryGetValue(zone, out var scan))
                continue;

            message.Append("    ");
            message.Append(Loc.GetString(
                "dwarfmed-part-status-line",
                ("part", Loc.GetString(GetZoneLocKey(zone))),
                ("status", Describe(scan, dead))));
            message.Append('\n');
        }

        return message.ToString().TrimEnd();
    }

    private string Describe(DwarfBodyPartScan scan, bool dead)
    {
        var statuses = new List<string>();

        var severity = DwarfPartStatus.GetSeverity(scan, dead);
        if (severity != DwarfPartSeverity.Healthy)
            statuses.Add(LocalizeSeverity(severity));

        if (!scan.Severed && !dead)
        {
            var brute = scan.SlashDamage + scan.BruteDamage;
            if (brute > FixedPoint2.Zero)
            {
                var ratio = MathF.Max(
                    GetRatio(scan.SlashDamage, scan.SlashThreshold),
                    GetRatio(scan.BruteDamage, scan.BruteThreshold));
                statuses.Add(Loc.GetString(ratio switch
                {
                    < 0.34f => "dwarfmed-part-status-bruised",
                    < 0.67f => "dwarfmed-part-status-battered",
                    _ => "dwarfmed-part-status-heavily-bruised",
                }));
            }

            if (scan.BurnDamage > FixedPoint2.Zero)
            {
                var ratio = GetRatio(scan.BurnDamage, scan.BurnThreshold);
                statuses.Add(Loc.GetString(ratio switch
                {
                    < 0.34f => "dwarfmed-part-status-red",
                    < 0.67f => "dwarfmed-part-status-blistering",
                    _ => "dwarfmed-part-status-charred",
                }));
            }

            if (scan.Fractured)
                statuses.Add(Loc.GetString("dwarfmed-part-status-fractured"));

            if (scan.Splinted)
                statuses.Add(Loc.GetString("dwarfmed-part-status-splinted"));

            if (scan.InternalBleeding)
                statuses.Add(Loc.GetString("dwarfmed-part-status-internal-bleeding"));

            if (scan.StumpBleeding)
                statuses.Add(Loc.GetString("dwarfmed-part-status-stump-bleeding"));
        }

        return JoinStatuses(statuses);
    }

    private string JoinStatuses(List<string> statuses)
    {
        if (statuses.Count == 0)
            return Loc.GetString("dwarfmed-part-status-fine");

        if (statuses.Count == 1)
            return statuses[0];

        var last = statuses[^1];
        return string.Join(
            Loc.GetString("dwarfmed-part-status-comma"),
            statuses.Take(statuses.Count - 1).Append(
                Loc.GetString("dwarfmed-part-status-and", ("status", last))));
    }

    private string LocalizeSeverity(DwarfPartSeverity severity)
    {
        return severity switch
        {
            DwarfPartSeverity.LightlyWounded => Loc.GetString("dwarfmed-part-status-injured"),
            DwarfPartSeverity.SomewhatWounded => Loc.GetString("dwarfmed-part-status-hurt"),
            DwarfPartSeverity.ModeratelyWounded => Loc.GetString("dwarfmed-part-status-badly-damaged"),
            DwarfPartSeverity.HeavilyWounded => Loc.GetString("dwarfmed-part-status-severely-damaged"),
            DwarfPartSeverity.CriticallyWounded => Loc.GetString("dwarfmed-part-status-mangled"),
            DwarfPartSeverity.Severed => Loc.GetString("dwarfmed-part-status-missing"),
            DwarfPartSeverity.Dead => Loc.GetString("dwarfmed-part-status-lifeless"),
            DwarfPartSeverity.Disabled => Loc.GetString("dwarfmed-part-status-disabled"),
            _ => Loc.GetString("dwarfmed-part-status-fine"),
        };
    }

    private string GetZoneLocKey(BodyZone zone)
    {
        return zone switch
        {
            BodyZone.Head => "dwarfmed-body-zone-head",
            BodyZone.Chest => "dwarfmed-body-zone-chest",
            BodyZone.Groin => "dwarfmed-body-zone-groin",
            BodyZone.LeftArm => "dwarfmed-body-zone-left-arm",
            BodyZone.RightArm => "dwarfmed-body-zone-right-arm",
            BodyZone.LeftHand => "dwarfmed-body-zone-left-hand",
            BodyZone.RightHand => "dwarfmed-body-zone-right-hand",
            BodyZone.LeftLeg => "dwarfmed-body-zone-left-leg",
            BodyZone.RightLeg => "dwarfmed-body-zone-right-leg",
            BodyZone.LeftFoot => "dwarfmed-body-zone-left-foot",
            BodyZone.RightFoot => "dwarfmed-body-zone-right-foot",
            _ => "dwarfmed-body-zone-chest",
        };
    }

    private static float GetRatio(FixedPoint2 damage, FixedPoint2 threshold)
    {
        return threshold > FixedPoint2.Zero ? damage.Float() / threshold.Float() : 0f;
    }
}
