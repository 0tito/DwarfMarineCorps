# Phase 1 Implementation Guide

Phase: Body-first damage, limb accumulators, selection UI, and event-driven
delimbing
Status: Complete
Branch: `EvilMed`
Related plan: [implementation-plan.md](implementation-plan.md)
Related design: [design.md](design.md)
Current implementation map: [current-state.md](current-state.md)

This guide replaces the old separate Phase 1a and Phase 1b. It includes the body
selector and the first limb-treatment hook from the start.

## 0. Model summary

The body always takes the full incoming damage.

The targeted limb receives a percentage of that same damage as local delimb
progress:

```text
LimbDamage = IncomingDamage * LimbDamageShare
```

Default:

```text
LimbDamageShare = 0.5
```

This means:

- Limbs are not extra HP.
- The body is not tankier.
- Spread damage still kills normally.
- Focused limb damage is what causes delimbing.
- A 1000 damage hit still deals 1000 body damage and can still delimb the limb.

Limb accumulators are separate counters by damage family:

- `Slash` -> `SlashDamage`
- `Blunt` + `Piercing` -> `BruteDamage`
- `Heat` + `Shock` + `Cold` + `Caustic` -> `BurnDamage`

There is no `SystemicBrute`, no `LostLimbBrute`, and no reconciliation loop.

## 1. Components

### 1.1 `DwarfAnatomyComponent`

File:

`Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfAnatomyComponent.cs`

Add:

```csharp
using Content.Shared.Body.Part;

[DataField] public float LimbDamageShare = 0.5f;
[DataField] public bool AllowDelimbing = true;

[DataField] public Dictionary<BodyPartType, float> HitWeights = new()
{
    [BodyPartType.Head] = 10f,
    [BodyPartType.Torso] = 30f,
    [BodyPartType.Groin] = 10f,
    [BodyPartType.Arm] = 16f,
    [BodyPartType.Hand] = 8f,
    [BodyPartType.Leg] = 20f,
    [BodyPartType.Foot] = 8f,
};
```

`HitWeights` are total weights per `BodyPartType`. Split symmetrical weights
evenly among attached parts of that type.

They are used only for:

- NPC attacks with no `BodyZoneTargetComponent`
- fallback when no selected part can be found

Player-controlled mobs, including xenos, use their selected zone directly.
The default selected zone is Chest.

### 1.2 `DwarfBodyPartComponent`

Add:

```csharp
[DataField, AutoNetworkedField] public bool Dismemberable = true;

[DataField, AutoNetworkedField] public FixedPoint2 MaxLocalDamage = 300f;

[DataField, AutoNetworkedField] public FixedPoint2 SlashDamage;
[DataField, AutoNetworkedField] public FixedPoint2 BruteDamage;
[DataField, AutoNetworkedField] public FixedPoint2 BurnDamage;

[DataField, AutoNetworkedField] public FixedPoint2 SlashThreshold = 80f;
[DataField, AutoNetworkedField] public FixedPoint2 BruteThreshold = 120f;
[DataField, AutoNetworkedField] public FixedPoint2 BurnThreshold = 300f;

[DataField, AutoNetworkedField] public bool DismemberAttempted;

[DataField, AutoNetworkedField] public float StumpBleedRate = 0.3f;
```

Keep the existing fracture/organ fields for Phase 2.

Suggested YAML overrides:

| Part | Slash | Brute | Burn | Stump bleed |
|---|---:|---:|---:|---:|
| Head | 300 | 500 | 1000 | 1.0 |
| Chest | 100 | 150 | 300 | 0 |
| Groin | 80 | 120 | 300 | 0 |
| Arm | 80 | 120 | 300 | 0.6 |
| Hand | 80 | 120 | 300 | 0.3 |
| Leg | 80 | 120 | 300 | 0.6 |
| Foot | 80 | 120 | 300 | 0.3 |

`MaxLocalDamage` is only a counter cap. It is not limb HP and never reduces body
damage.

### 1.3 `BodyZone`

Add serialization:

```csharp
[Serializable, NetSerializable]
public enum BodyZone : byte
```

### 1.4 Add events

`BodyZoneSelectedEvent.cs`:

```csharp
[Serializable, NetSerializable]
public sealed class BodyZoneSelectedEvent(BodyZone zone) : EntityEventArgs
{
    public readonly BodyZone Zone = zone;
}
```

`DwarfDelimbAttemptEvent.cs`:

```csharp
[ByRefEvent]
public struct DwarfDelimbAttemptEvent
{
    public EntityUid Body;
    public EntityUid Part;
    public DwarfSeverReason Reason;
    public DamageSpecifier Damage;
    public EntityUid? Origin;
    public EntityUid? Tool;
    public BodyZone Zone;
    public float Chance;
    public bool Cancelled;
    public bool Forced;
}
```

`DwarfSeverRequestEvent.cs`:

```csharp
[ByRefEvent]
public struct DwarfSeverRequestEvent
{
    public EntityUid Body;
    public EntityUid Part;
    public DwarfSeverReason Reason;
    public EntityUid? Origin;
    public EntityUid? Tool;
    public bool Handled;
}
```

`DwarfSeverReason.cs`:

```csharp
public enum DwarfSeverReason : byte
{
    Damage,
    Surgery,
    Explosion,
    Fire,
    Special,
}
```

`DwarfBleedingEvent.cs`:

```csharp
[ByRefEvent]
public record struct DwarfBleedingEvent(float Amount);
```

## 2. Zone target

Do not add the component to the dwarf YAML. Use a player-attachment system so all
player-controlled mobs get it, including xenos, while NPCs do not.

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/BodyZoneTargetSystem.cs`

```csharp
public sealed class BodyZoneTargetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<BodyZoneSelectedEvent>(OnSelected);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Entity) ||
            !HasComp<HandsComponent>(args.Entity))
        {
            return;
        }

        EnsureComp<BodyZoneTargetComponent>(args.Entity);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (!TerminatingOrDeleted(args.Entity))
            RemComp<BodyZoneTargetComponent>(args.Entity);
    }

    private void OnSelected(BodyZoneSelectedEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!Enum.IsDefined(ev.Zone) ||
            !TryComp<BodyZoneTargetComponent>(player, out var target))
        {
            return;
        }

        target.SelectedZone = ev.Zone;
        Dirty(player, target);
    }
}
```

`SelectedZone` defaults to `BodyZone.Chest`.

## 3. Client selector

Use the existing UI:

- `Content.Client/_DwarfMC/Medical/UI/DwarfMedicalBodyOverlay.cs`
- `Content.Client/_DwarfMC/Medical/UI/DwarfMedicalBodyOverlay.xaml`
- `Content.Client/_DwarfMC/Medical/UI/DwarfMedicalBodyOverlayUIController.cs`

Keep the current XAML layout and art unchanged. Only wire the code:

- Use `BodyZone` instead of the local UI enum.
- Add `SetSelected(BodyZone)` for server sync.
- Send `BodyZoneSelectedEvent` on click.
- Hide the overlay unless the local player has `BodyZoneTargetComponent`.
- Read `SelectedZone` every frame and update the highlight.

Anchor remains bottom-right with the current 192x192 layout.

### 3.1 Textures

The current raw PNGs are used:

`Resources/Textures/_DwarfMC/Interface/Medical/BodySelection/`

States are the existing files:

```text
base.png
groin.png
groin_hover.png
head.png
head_hover.png
chest.png
chest_hover.png
left_arm.png
left_arm_hover.png
right_arm.png
right_arm_hover.png
left_hand.png
left_hand_hover.png
right_hand.png
right_hand_hover.png
left_leg.png
left_leg_hover.png
right_leg.png
right_leg_hover.png
left_foot.png
left_foot_hover.png
right_foot.png
right_foot_hover.png
```

Rendering:

- `Base` is the bottom `TextureRect`.
- Each region has an overlay `TextureRect`.
- Only the selected region overlay is visible.
- Selected overlay opacity is left as authored; 70% tuning is deferred.
- Invisible `Button`s over each region handle clicks.
- All regions stay clickable. If the target lacks the selected part, the server
  falls back to the parent part, then weighted random.
- No Random button. The default selected zone is Chest.
- Show the selector for any local player with `BodyZoneTargetComponent`, including
  xenos. Hide it for ghosts and entities without the component.

Aseprite export:

```bash
aseprite -b --all-layers body_zones.ase --save-as '{layer}.png'
```

Keep all layers on the same canvas size. Do not trim them individually.

## 4. Part lookup and hit weights

Add a shared helper:

`Content.Shared/_DwarfMC/Medical/DwarfMed/BodyZoneSystem.cs`

```csharp
public bool TryGetPartForZone(
    EntityUid body,
    BodyZone zone,
    out EntityUid part)
```

Mapping:

| Zone | Part |
|---|---|
| Head | `BodyPartType.Head` |
| Chest | `BodyPartType.Torso` |
| Groin | `BodyPartType.Groin` |
| LeftArm / RightArm | `BodyPartType.Arm` + symmetry |
| LeftHand / RightHand | `BodyPartType.Hand` + symmetry |
| LeftLeg / RightLeg | `BodyPartType.Leg` + symmetry |
| LeftFoot / RightFoot | `BodyPartType.Foot` + symmetry |
| No valid selected part | weighted random fallback |

Fallback:

```text
Hand -> Arm -> Chest
Foot -> Leg -> Groin -> Chest
```

Weighted random:

1. Enumerate attached parts.
2. Get the type weight from `HitWeights`.
3. Divide by the number of attached parts of that type.
4. Pick a weighted random part.

## 5. Damage routing

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfBodyPartDamageSystem.cs`

Subscribe:

```csharp
SubscribeLocalEvent<DwarfAnatomyComponent, DamageChangedEvent>(OnDamageChanged);
```

Server-only:

```csharp
if (!_net.IsServer || args.DamageDelta == null)
    return;
```

For positive Brute/Burn damage:

1. Pick the part using the origin's selected zone, or weighted random.
2. Apply the body's damage normally. The body already took it in the normal
   pipeline; do not reduce it.
3. Add a share of the damage to the part accumulators:

```text
SlashDamage += incomingSlash * LimbDamageShare
BruteDamage += (incomingBlunt + incomingPiercing) * LimbDamageShare
BurnDamage  += incomingBurn * LimbDamageShare
```

4. Cap each accumulator at `MaxLocalDamage`.
5. If any accumulator crosses its threshold, raise
   `DwarfDelimbAttemptEvent` with `Chance = 1`, `Forced = true`,
   `Reason = Damage`.
6. If the event is not cancelled, raise `DwarfSeverRequestEvent`.

Do not route Toxin, Airloss, or Genetic damage to limb accumulators.

For negative body damage:

- Body healing is normal.
- If the healing origin is a doctor with a selected zone, reduce the selected
  part's accumulators. See section 6.

## 6. Healing accumulators

Body healing remains normal global damage healing.

When a doctor with a selected body zone heals a patient:

- The body heals normally.
- The selected part's accumulators are also reduced.
- `LimbHealShare` controls how much of the healing also applies to local
  accumulators. Default: `1.0`.
- Brute healing reduces `SlashDamage` and `BruteDamage`.
- Burn healing reduces `BurnDamage`.
- If several relevant accumulators exist, distribute the limb healing between
  them proportionally.

If the body is already at full health but the limb still has accumulator damage,
allow limb-only treatment on the selected zone. Add a `DwarfLimbHealerComponent`
to the relevant medical items, or a dedicated limb-treatment path.

Recommended Phase 1 behavior:

- `CMGauze` / `CMOintment` and similar items heal the selected limb's
  accumulators when used by a doctor with a selected zone.
- A doctor can continue using the item on the selected limb even when the body
  is at full health.
- Chems heal the body only in Phase 1. Later phases can extend hyposprays or
  automatic treatment.

## 7. Delimbing

### 7.1 Baseline policy

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfDelimbPolicySystem.cs`

Subscribe to `DwarfDelimbAttemptEvent`.

For `Reason == Damage`:

```text
if !part.Dismemberable:
    return

if part.SlashDamage >= part.SlashThreshold:
    ev.Chance = 1
    ev.Forced = true
    return

if part.BruteDamage >= part.BruteThreshold:
    ev.Chance = 1
    ev.Forced = true
    return

if part.BurnDamage >= part.BurnThreshold:
    ev.Chance = 1
    ev.Forced = true
    return
```

Other systems can:

- Cancel the attempt.
- Modify `Chance`.
- Replace the default rule.
- Add weapon-specific or source-specific rules.
- Add post-death burn rules.

Use `part.DismemberAttempted` to avoid raising the same attempt every tick after
the threshold is reached. Reset it when accumulators drop below all thresholds
or when the limb is reattached.

### 7.2 Sever system

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfSeverSystem.cs`

Subscribe to `DwarfSeverRequestEvent`.

For actual detaching, add a partial helper under `_DwarfMC`:

`Content.Shared/_DwarfMC/Medical/DwarfMed/SharedBodySystem.DwarfSever.cs`

This partial can access protected body removal logic without editing the upstream
`SharedBodySystem` file.

Sever flow:

1. Raise a cancellable `BeforeDwarfPartSeveredEvent`.
2. Verify the part is attached.
3. Remove descendant hand/foot parts so hand slots and sprite layers update.
4. Drop each detached part separately, so an arm drops with its hand and a leg
   drops with its foot as separate items.
5. Non-fire limbs are thrown in a random direction with a fleshy delimb sound.
   If the reason is `Fire`, delete the detached part, play the ashing sound, and
   spawn an `Ash` entity instead of dropping a limb item.
6. Add `DwarfSeveredLimbComponent` to each dropped part.
6. Discard the limb's local accumulators with the limb. Global damage is not
   changed by the sever itself.
7. Add stump bleeding to the parent part condition.
8. Ensure `WoundedComponent` exists so bleeding updates run.
9. Play sound, popups, and blood effects.

### 7.3 Per-part behavior

| Part | Behavior |
|---|---|
| Head | Death, unrevivable, block mind transfer |
| Chest | Never sever |
| Groin | Never sever |
| Arm | Hand also detaches and drops separately; hand slot and sprite removed |
| Hand | Hand slot and sprite removed |
| Leg | Foot also detaches and drops separately; movement penalty |
| Foot | Movement penalty |

### 7.4 Decapitation

Patch:

`Content.Server/Body/Systems/BrainSystem.cs`

When `OrganRemovedFromBodyEvent` fires, check the brain's parent body part. If
that parent is no longer attached to a body, skip mind transfer. This keeps
normal brain surgery working while preventing decapitation from creating a living
brain item.

## 8. Stump bleeding

Patch:

`Content.Server/_RMC14/Medical/Wounds/WoundsSystem.cs`

After the wound bloodloss calculation:

```csharp
var bleedingEv = new DwarfBleedingEvent(0f);
RaiseLocalEvent(uid, ref bleedingEv);
bloodloss += bleedingEv.Amount;
```

Keep `WoundedComponent` alive while `bleedingEv.Amount > 0`.

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfBleedingSystem.cs`

It subscribes to `DwarfBleedingEvent` and sums:

- All `StumpBleeding` rates from `DwarfBodyPartConditionComponent`.
- Internal bleeding later in Phase 2.

## 9. Movement

Create:

`Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfMovementSystem.cs`

Subscribe to `RefreshMovementSpeedModifiersEvent` on `DwarfAnatomyComponent`.

Phase 1 values:

| Condition | Multiplier |
|---|---:|
| One foot missing | 0.85 |
| Both feet missing | 0.7 |
| One leg missing | Existing body speed math already reduces speed |
| Both legs missing | Existing knockdown behavior |

Do not double-penalize leg loss.

## 10. Scanner

Extend `HealthScanState`:

```csharp
List<DwarfBodyPartScan> Parts;
```

Add:

```csharp
public readonly record struct DwarfBodyPartScan(
    string SlotId,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    FixedPoint2 SlashDamage,
    FixedPoint2 BruteDamage,
    FixedPoint2 BurnDamage,
    FixedPoint2 SlashThreshold,
    FixedPoint2 BruteThreshold,
    FixedPoint2 BurnThreshold,
    bool Bleeding,
    bool StumpBleeding,
    bool Severed);
```

Update:

- `Content.Shared/_RMC14/Medical/Scanner/HealthScannerSystem.cs`
- `Content.Server/_RMC14/RMCMedicalRecords/RMCMedicalRecordsSystem.cs`
- `Content.Client/_RMC14/Medical/Scanner/HealthScannerUiData.cs`
- `Content.Client/_RMC14/Medical/Scanner/HealthScannerWindow.xaml`

Analyzer rows:

```text
Head        Slash 25/300  Brute 0/500  Burn 0/1000
Left hand   Slash 80/80   DELIMB THRESHOLD
Right leg   Slash 30/80   Brute 15/120
Right arm   MISSING
```

## 11. Upstream edits

New ones for Phase 1:

1. `Content.Server/_RMC14/Medical/Wounds/WoundsSystem.cs`
   - Raise `DwarfBleedingEvent`.
   - Include its amount in bloodloss.
   - Keep `WoundedComponent` while NewMed bleeding is active.

2. `Content.Server/Body/Systems/BrainSystem.cs`
   - Skip mind transfer when the brain's parent part is detached.

3. `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`
   - Add self-sublayers for hands and feet so direct hand/foot severing hides
     their sprite layers.

`DwarfMedSetupSystem` removes `MaxDamageComponent` from DwarfMed dwarfs at
MapInit so corpses continue taking damage past the old 719 cap.

## 12. Implementation order

1. Add `LimbDamageShare`, `HitWeights`, and accumulator fields.
2. Add events and zone selection.
3. Add the selector UI and textures.
4. Add part lookup and hit weights.
5. Add damage routing and accumulator application.
6. Add delimb attempt/policy.
7. Add `DwarfSeverSystem`.
8. Add stump bleeding.
9. Add healing accumulator reduction and limb-only treatment.
10. Add movement penalties.
11. Extend the scanner.
12. Add and run tests.

## 13. Tests

Add tests under:

`Content.IntegrationTests/_DwarfMC/DwarfMed/`

Cases:

- Selected zone routes limb share to the correct part.
- Selected zone overrides hit weights.
- Weighted NPC/fallback selection uses hit weights.
- Body damage is never reduced by limb accumulators.
- Slash/brute/burn accumulators track separately.
- Threshold crossing raises the delimb event.
- Hand sever removes the hand slot.
- Arm sever removes the hand as well.
- Chest and groin never sever.
- Delimbing adds no direct global damage.
- Foot loss slows movement.
- Head sever kills and makes the patient unrevivable.
- Doctor-focused healing reduces the selected limb accumulators.
- Limb-only healing works when the body is at full health.
- Non-dwarf targets are unaffected by DwarfMed damage routing; player xenos still get the selector.
- Scanner contains limb accumulator data.

Run:

```bash
dotnet test --no-build --configuration DebugOpt \
  Content.IntegrationTests/Content.IntegrationTests.csproj \
  --filter "FullyQualifiedName~DwarfMed" \
  -- NUnit.ConsoleOut=0
```

The filter must come before `--`.

## 14. Gotchas

- The body always takes full damage. Do not subtract limb accumulator damage from
  the body.
- Do not add extra direct damage when severing.
- Do not let the global `WoundsSystem` overwrite stump bleeding.
- Do not let decapitation transfer the mind into the brain item.
- Do not route Toxin/Airloss/Genetic damage to limb accumulators.
- Do not use `HitWeights` when a specific zone is selected.
- Keep the filter before `--` when running `dotnet test`.
- Keep the body-zone textures on the same canvas size.
- Reset `DismemberAttempted` when accumulators drop below their thresholds.
