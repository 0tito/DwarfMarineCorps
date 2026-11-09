# DwarfMed (NewMed) Design Document

Status: Phase 0, Phase 1, and Phase 2 complete; Phase 3 next
Branch: `EvilMed`
Scope: Dwarf-only per-limb medical system

Naming note: `NewMed` is the design name. The implementation uses `DwarfMed`,
`DwarfMedBaseMobSpeciesOrganic`, and the body prototype `DMCDwarf`.

## 1. Summary

DwarfMed replaces the current body-global medical model with a per-region anatomy
model for dwarfs.

The body is divided into:

- Head
- Chest
- Groin
- Left and right upper arms
- Left and right hands
- Left and right legs
- Left and right feet

The body always takes full damage. The targeted limb receives a percentage of
that damage as local delimb progress. When a limb accumulator crosses its
threshold, a delimb event is raised. Chest and groin cannot be severed. Severing
a limb causes no extra damage, only stump bleeding and loss of function.

The system also adds:

- Fractures
- Internal bleeding
- Internal organs with health and effects
- A bottom-right body-zone selector used for attack targeting and treatment
- New surgical procedures for bones, internal bleeding, stumps, and organs

Existing global health remains the source of truth for critical and death states.
For DwarfMed dwarfs, the thresholds are 100 critical and 200 dead.

## 2. Goals

- Make limb damage meaningful without requiring a full medical rewrite.
- Make delimbing a tactical consequence rather than a damage spike.
- Keep existing armor, chemistry, defibrillator, and mob-state systems working.
- Keep DwarfMed dwarf-only and merge-friendly for upstream RMC14 updates.
- Provide clear diagnostics and treatment paths for every new condition.
- Keep all tuning data-driven through YAML and CVars.

## 3. Non-goals for v1

- Blood types.
- Pain system.
- Shrapnel and foreign objects.
- Eschar and severe burn states.
- Infections.
- Full limb-specific wound treatment.
- Non-dwarf species anatomy.
- Xeno, synth, or robotic anatomy.
- Full chemistry rebalance.

## 4. Current codebase anchors

These existing systems are reused or extended:

| System | Location | Relevance |
|---|---|---|
| Body tree | `Content.Shared/Body/Systems/SharedBodySystem.Body.cs` | Spawns body parts from `BodyPrototype` |
| Body part component | `Content.Shared/Body/Part/BodyPartComponent.cs` | Part type, symmetry, children, organs |
| Part types | `Content.Shared/Body/Part/BodyPartType.cs` | Needs `Groin` added |
| Hand slots | `Content.Server/Hands/Systems/HandsSystem.cs` | Adds/removes hand slots on hand part add/remove |
| Humanoid layers | `Content.Server/Body/Systems/BodySystem.cs` | Hides sprite layers on part removal |
| Movement | `Content.Shared/Body/Systems/SharedBodySystem.Parts.cs` | Legs affect base speed; feet currently do not |
| Global damage | `Content.Shared/Damage/Systems/DamageableSystem.cs` | `DamageChangedEvent` with `Origin` and `Tool` |
| Global wounds | `Content.Shared/_RMC14/Medical/Wounds/*` | Existing brute/burn wound and bloodloss layer |
| Bleeding | `Content.Shared/Body/Components/BloodstreamComponent.cs` | `BleedAmount` consumed by bloodstream system |
| Surgery | `Content.Shared/_RMC14/Medical/Surgery/*` | Data-driven steps, conditions, and effects |
| Health scanner | `Content.Shared/_RMC14/Medical/Scanner/*` | `HealthScanState` and analyzer UI |
| Body scanner | `Content.Shared/_RMC14/Medical/BodyScanner/*` | Scan snapshots and medical records |
| Defibrillator | `Content.Shared/_RMC14/Medical/Defibrillator/*` | Revive flow and blocked-defib states |
| Organs | `Content.Shared/Body/Organ/OrganComponent.cs` | Organ container membership only; no health |

## 5. Merge-safe architecture

### 5.1 Files and namespaces

All DwarfMed code and data lives under the `_DwarfMC` prefix. Phase 0 uses these
paths; later phases extend them rather than moving them:

```text
Content.Shared/_DwarfMC/Medical/DwarfMed/
Content.Server/_DwarfMC/Medical/DwarfMed/
Content.Client/_DwarfMC/Medical/UI/

Resources/Prototypes/_DwarfMC/Body/
  Prototypes/dwarf.yml
  Parts/DwarfMedParts.yml
  Organs/Organs.yml
Resources/Prototypes/_DwarfMC/Entities/Mobs/Species/BaseDwarf.yml
Resources/Textures/_DwarfMC/Interface/Medical/BodySelection/
Resources/Locale/en-US/_DwarfMC/dwarfmed.ftl
docs/newmed/
```

### 5.2 Dwarf-only attachment

Do not edit the shared `RMCBaseMobSpeciesOrganic`. Do not use runtime species
detection. Use a dwarf-only intermediate parent.

```text
BaseMobSpecies
└── RMCBaseMobSpeciesOrganic
    ├── CMMobHuman
    ├── CMMobMoth
    ├── CMMobDwarf
    └── ...other humanoids

DwarfMedBaseMobSpeciesOrganic (new, abstract, in _DwarfMC)
└── CMMobDwarf (reparented)
```

`DwarfMedBaseMobSpeciesOrganic` inherits `RMCBaseMobSpeciesOrganic` and
overrides:

```yaml
- type: entity
  abstract: true
  save: false
  parent: RMCBaseMobSpeciesOrganic
  id: DwarfMedBaseMobSpeciesOrganic
  components:
  - type: Body
    prototype: DMCDwarf
    requiredLegs: 2
  - type: DwarfAnatomy
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
```

The only upstream change needed for attachment:

```yaml
# Resources/Prototypes/_RMC14/Entities/Mobs/Species/dwarf.yml
- type: entity
  save: false
  parent: DwarfMedBaseMobSpeciesOrganic
  id: CMMobDwarf
```

### 5.3 Xenos are unaffected

Xenos do not inherit `RMCBaseMobSpeciesOrganic`.

`CMXenoBase` is a separate root entity in
`Resources/Prototypes/_RMC14/Entities/Mobs/Xeno/base_xeno.yml` and uses its own
`Body` prototype (`Xenonid`). Reparenting `CMMobDwarf` has no effect on xenos.

Xenos do use the generic `BodyZoneTargetComponent` and the body-zone selector so
players can aim their attacks. They do not use DwarfMed anatomy, limb
accumulators, delimbing, or organ health.

### 5.4 Upstream edits

The complete list of upstream edits expected for NewMed:

1. `Content.Shared/Body/Part/BodyPartType.cs`: append `Groin`.
2. `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`: handle `Groin`.
3. `Content.Client/_RMC14/Medical/Surgery/CMSurgeryBui.cs`: sort order for `Groin`.
4. `Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.Steps.cs`:
   slot mapping for `Groin`.
5. `Resources/Prototypes/_RMC14/Entities/Mobs/Species/dwarf.yml`: reparent
   `CMMobDwarf` to `DwarfMedBaseMobSpeciesOrganic`.
6. Phase 1: `Content.Server/_RMC14/Medical/Wounds/WoundsSystem.cs`: include
   `DwarfBleedingEvent` in the bloodloss calculation so stump bleeding is not
   overwritten by the global wound update.
7. Phase 1: `Content.Server/Body/Systems/BrainSystem.cs`: skip mind transfer
   when the brain's parent body part is detached, so decapitation does not turn
   the player into a living brain item.
8. Phase 1: `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`: add
   self-sublayers for hands and feet so direct hand/foot severing hides their
   sprite layers.
9. DwarfMed removes `MaxDamageComponent` at runtime in `DwarfMedSetupSystem`,
   allowing post-death damage and burn delimbing to continue past the old 719
   cap.
10. Phase 1 scanner/records integration:
    - `Content.Shared/_RMC14/Medical/Scanner/HealthScannerUI.cs`
    - `Content.Shared/_RMC14/Medical/Scanner/HealthScannerSystem.cs`
    - `Content.Server/_RMC14/RMCMedicalRecords/RMCMedicalRecordsSystem.cs`
    - `Content.Client/_RMC14/Medical/Scanner/HealthScannerUiData.cs`
    - `Content.Client/_RMC14/Medical/Scanner/HealthScannerWindow.xaml`

Everything else is additive under `_DwarfMC`.

## 6. Anatomy model

### 6.1 Body tree

```text
Chest (Torso, root)
├── Head
├── Left Arm
│   └── Left Hand
├── Right Arm
│   └── Right Hand
└── Groin
    ├── Left Leg
    │   └── Left Foot
    └── Right Leg
        └── Right Foot
```

The body prototype is `DMCDwarf`, defined in `_DwarfMC`. It references the
`DwarfMed*` part prototypes that inherit the existing dwarf/human sprites and
body-part behavior.

### 6.2 Body part types

`BodyPartType` gains `Groin`. Existing values must not be reordered because they
are serialized.

```csharp
public enum BodyPartType
{
    Other = 0,
    Torso,
    Head,
    Arm,
    Hand,
    Leg,
    Foot,
    Tail,
    Groin
}
```

`Groin` maps to no humanoid sprite layer because the chest/legs already cover
that visual region. Severing the groin is not allowed, so no separate stump
sprite is needed.

### 6.3 Organs

| Region | Organs |
|---|---|
| Head | brain, eyes |
| Chest | heart, lungs, liver, stomach |
| Groin | kidneys |

`DwarfMed*` organ prototypes inherit the existing dwarf/human organ behavior and
add `DwarfOrganHealthComponent`.

## 7. Damage model

### 7.1 Principles

- The body always takes the full incoming damage.
- The targeted limb receives a percentage of that same damage as local delimb
  progress.
- Limbs are not extra HP. They do not absorb damage away from the body.
- Limb accumulators are separate counters used for delimbing, not health pools.
- Delimbing is event-driven.
- There is no global/local damage ledger, no `SystemicBrute`, no `LostLimbBrute`,
  and no reconciliation loop.

This keeps existing body health, chemistry, armor, and crit/death behavior
intact while making delimbing rarer than raw body damage.

### 7.2 Body-first, limb-share

`DwarfAnatomyComponent` stores the default share:

```csharp
public sealed partial class DwarfAnatomyComponent : Component
{
    [DataField] public bool Enabled = true;
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
}
```

On a hit:

1. The body takes the full damage through the normal damage pipeline.
2. The targeted part receives `incoming damage * LimbDamageShare`.
3. The local share is split by damage family:
   - `Slash` -> `SlashDamage`
   - `Blunt` and `Piercing` -> `BruteDamage`
   - `Heat`, `Shock`, `Cold`, and `Caustic` -> `BurnDamage`
4. Accumulators are capped at `MaxLocalDamage`.
5. If any accumulator crosses its threshold, a delimb attempt is raised.

Example with `LimbDamageShare = 0.5`:

```text
1000 slash damage to the arm:
- body takes 1000
- arm SlashDamage gains 500
- arm accumulator is capped at MaxLocalDamage
- SlashThreshold is crossed
- delimb attempt is raised
```

```text
50 slash damage to the arm:
- body takes 50
- arm SlashDamage gains 25
- no delimb
```

Spread damage does not make the body tankier because the body still takes 100%.

### 7.3 Limb accumulators and thresholds

`DwarfBodyPartComponent` stores:

```csharp
public sealed partial class DwarfBodyPartComponent : Component
{
    [DataField] public bool Dismemberable = true;

    [DataField] public FixedPoint2 MaxLocalDamage = 300f;

    [DataField] public FixedPoint2 SlashDamage;
    [DataField] public FixedPoint2 BruteDamage;
    [DataField] public FixedPoint2 BurnDamage;

    [DataField] public FixedPoint2 SlashThreshold = 80f;
    [DataField] public FixedPoint2 BruteThreshold = 120f;
    [DataField] public FixedPoint2 BurnThreshold = 300f;

    [DataField] public bool DismemberAttempted;

    [DataField] public FixedPoint2 FractureScale = 100f;
    [DataField] public FixedPoint2 OrganDamageThreshold = 25f;
    [DataField] public float InternalBleedRate = 0.2f;
    [DataField] public float BleedMultiplier = 1f;
    [DataField] public float FractureChanceMultiplier = 1f;
    [DataField] public float InternalBleedChanceMultiplier = 1f;
    [DataField] public float StumpBleedRate = 0.3f;
}
```

Initial thresholds:

| Part | Slash | Brute | Burn | Dismemberable |
|---|---:|---:|---:|---|
| Head | 300 | 500 | 1000 | Yes |
| Chest | 100 | 150 | 300 | No |
| Groin | 80 | 120 | 300 | No |
| Arm | 80 | 120 | 300 | Yes |
| Hand | 80 | 120 | 300 | Yes |
| Leg | 80 | 120 | 300 | Yes |
| Foot | 80 | 120 | 300 | Yes |

`MaxLocalDamage` is only a counter cap. It is not limb HP and does not reduce
body damage.

### 7.4 Delimb event

When an accumulator crosses its threshold:

```text
if part is not dismemberable: no delimb
if part.DismemberAttempted: no delimb until accumulators drop below threshold
raise DwarfDelimbAttemptEvent with Chance = 1, Forced = true, Reason = Damage
```

Other systems can cancel, modify, or replace the behaviour. If the attempt is
not cancelled, it raises `DwarfSeverRequestEvent`.

When the target has `OnFireComponent` and takes burn damage, the burn share is
applied to every attached part instead of one random part. This uses
`DwarfSeverReason.Fire`.

Burn delimbing is allowed after death. Dead bodies remain damageable, so a
corpse can still be burned or shot until a limb chars off. A delimb caused by
the burn accumulator threshold uses the `Fire` reason even if the target was not
on fire, so it does not drop a limb item; the limb is deleted and an `Ash`
entity is spawned instead.

### 7.5 Healing accumulators

Body healing remains the normal global damage healing.

When a doctor has a selected body zone and heals a patient:

- The body heals normally.
- The selected part's accumulators are also reduced.
- `LimbHealShare` controls how much of the healing also applies to the local
  accumulators. Default: `1.0`.
- Brute healing reduces `SlashDamage` and `BruteDamage`.
- Burn healing reduces `BurnDamage`.
- If multiple relevant accumulators exist, distribute the limb healing between
  them proportionally.

If the body is already at full health but the limb still has accumulator damage,
a doctor can continue limb-only treatment on the selected zone. This is handled
by the limb treatment path rather than by normal body damage healing.

Chems currently heal the body only. Doctor-focused items can reduce limb
accumulators through the selected zone. Later phases can extend this to
hyposprays or automatic limb treatment.

### 7.6 Global thresholds

DwarfMed dwarfs use:

```yaml
MobThresholds:
  thresholds:
    0: Alive
    100: Critical
    200: Dead
```

Damage to any part contributes to global damage because the body always takes
the full hit.

## 8. Hit selection overlay

### 8.1 Purpose

One selector is used for both:

1. The zone the player's attacks target.
2. The zone a medic treats.

Raw damage is unchanged by zone selection in v1. The zone only determines where
local damage, fractures, delimbing attempts, and organ damage land.

### 8.2 Client UI

- The existing `DwarfMedicalBodyOverlay` control and its controller.
- Anchored bottom-right through a `UIController`; `DefaultGameScreen` is not
  edited.
- Displays a body silhouette with clickable regions:
  - Head
  - Chest
  - Groin
  - Left/right arm
  - Left/right hand
  - Left/right leg
  - Left/right foot
- Rendering model:
  - One `TextureRect` for `Base`.
  - One overlay `TextureRect` per region.
  - Only the selected region overlay is visible.
  - Selected overlay opacity is left as authored for now; 70% tuning is deferred.
- Current textures are raw PNGs under
  `Resources/Textures/_DwarfMC/Interface/Medical/BodySelection/`:
  `base.png`, region images, and matching `*_hover.png` images.
- Keep all layer PNGs on the same canvas size; do not trim individual layers or
  the overlays will not align.
- Invisible `Button`s are placed over the regions for clicks.
- All regions stay clickable. If the target lacks the selected part, the server
  falls back to the parent part, then to weighted random selection.
- The selector is visible for any player-controlled mob with a
  `BodyZoneTargetComponent`, including xenos. NPCs have no client and therefore
  no UI.
- The default selected zone is Chest. There is no Random button or clear state.
- The current overlay is not predicted. Clicking updates the local highlight and
  sends the selection to the server; the server component remains authoritative.
- Optional later: injury icons for fracture, bleeding, and organ damage.

### 8.3 Server state

`BodyZoneTargetComponent` on the player stores the selected `BodyZone`.

- Client sends a selection event.
- Server validates the player is alive and NewMed-enabled.
- Selection is replicated to the client.
- If the selected part is missing, the server falls back to the parent part, then
  to a weighted random attached part.

### 8.4 Attack routing

`DamageChangedEvent` carries `Origin`.

- If `Origin` has `BodyZoneTargetComponent`, use its `SelectedZone` directly.
- Projectiles use `ProjectileComponent.Shooter` as `Origin`, so guns work.
- Player-controlled xenos also have `BodyZoneTargetComponent`, so their attacks
  use their selected zone.
- NPC attacks have no selector and use weighted random selection.
- `HitWeights` are only consumed by weighted random selection. Symmetrical type
  weights are split evenly among attached parts of that type.
- If the selected part is missing, fall back to the parent part, then to
  weighted random selection.
- Special attacks can later provide an explicit zone, such as headbite.

### 8.5 Treatment targeting

Medical items and splints use the user's selected zone.

- Body healing remains normal.
- If the healer has a selected zone, the selected part's accumulators are also
  reduced.
- If the body is at full health, limb-only treatment can still reduce the
  selected part's accumulators.
- If the user has no selection, choose the most damaged valid part.
- Surgery continues to use the existing surgery UI part selection.

## 9. Delimbing

### 9.1 Common sever path

All severing goes through `DwarfSeverSystem`, which handles
`DwarfSeverRequestEvent`:

1. Raise a cancellable `BeforeDwarfPartSeveredEvent`.
2. Verify the part is attached to the body.
3. Remove descendant hand/foot parts so `BodyPartRemovedEvent` fires and hand
   slots and sprite layers update.
4. Drop each detached part separately, so an arm drops with its hand and a leg
   drops with its foot as separate limb items.
5. Non-fire limbs are thrown in a random direction with a fleshy delimb sound.
   If the reason is `Fire`, delete the detached part, play the ashing sound, and
   spawn an `Ash` entity instead of dropping a limb item.
6. Discard the part's local accumulators with the limb. Global damage is not
   changed by the sever itself.
7. Add stump bleeding to the parent part.
8. Add `DwarfSeveredLimbComponent` to each dropped limb.
8. Ensure `WoundedComponent` exists so the bleeding update runs.
9. Play audio, popups, and blood effects.

Damage-based delimbing does not call the sever path directly. It raises
`DwarfDelimbAttemptEvent`; after policy handlers and the chance roll, a successful
attempt raises `DwarfSeverRequestEvent`. Surgery and scripted events can raise
`DwarfSeverRequestEvent` directly.

The existing `HandsSystem` removes hand slots when hand parts are removed. The
existing `BodySystem` hides humanoid sprite layers when parts are removed.

### 9.2 Per-part behavior

| Part | Behavior |
|---|---|
| Head | Decapitation; death; unrevivable; block brain-to-mind transfer into the severed head |
| Chest | Cannot be severed |
| Groin | Cannot be severed |
| Arm | Hand detaches and drops separately; hand slot and sprite removed; heavy stump bleed |
| Hand | Hand slot and sprite removed |
| Leg | Foot detaches and drops separately; movement penalty; knockdown if both legs gone |
| Foot | Movement penalty |

### 9.3 Stump bleeding

Stump bleeding is a bleeding source, not instant damage.

- It is stored on the parent part's `DwarfBodyPartConditionComponent`.
- `DwarfBleedingSystem` sums stump rates and responds to `DwarfBleedingEvent`.
- `WoundsSystem.Update` raises `DwarfBleedingEvent` and includes its amount in
  the mob's bloodloss so the global wound update does not overwrite it.
- Gauze and trauma kits can pack the stump, halving the rate down to a minimum
  of `0.05`, but packing does not clear the bleeding.
- It is removed by the Seal Stump surgery.
- It continues until sealed.
- On dead dwarfs it continues at half rate.
- It can cause bloodloss critical/death if ignored.

### 9.4 Decapitation

Decapitation is the only severing case that directly causes death.

- Mark the body dead and unrevivable.
- Prevent the brain from transferring the player's mind into the severed head.
- Synthetic head reattachment is a future exception.

## 10. Fractures

### 10.1 Trigger

Fractures are chance-based and have no hard threshold.

- Slash and brute damage both count; burn does not.
- The roll uses the local trauma dealt by the hit and the total local trauma
  already on the part.

```text
localTrauma = (slash + brute) * LimbDamageShare
limbTrauma  = SlashDamage + BruteDamage

impact = localTrauma / FractureScale
load   = limbTrauma / FractureScale

chance = clamp((impact^2 + impact * load)
               * FractureChanceMultiplier
               * fractureChanceCVar, 0, 1)
```

`FractureScale` replaces the old `FractureThreshold`.

### 10.2 Effects

| Part | Effect |
|---|---|
| Arm/hand | Chance to drop held items on use, movement, or attack |
| Leg/foot | Movement penalty; unsplinted movement can cause internal bleeding |
| Chest | Organ-damage risk on movement |
| Head | Brain and eye damage risk |
| Groin | Kidney damage risk |

### 10.3 Splints

- Splint item applies `Splinted`.
- Splinted blocks the negative effects of the fracture.
- Splinted does not heal the fracture.
- Bone Repair surgery removes the fracture.
- Open incisions add a slow bleed until the incision is closed.
- A splint can be removed with a verb or by surgery.

## 11. Internal bleeding

### 11.1 Causes

- Armor-piercing hits.
- Unsplinted fracture movement.
- Severe chest or groin brute damage.
- Explosions.

### 11.2 Effects

- Adds ongoing bloodloss.
- Not stopped by gauze, ointment, or normal external bleeding treatment.
- Requires Internal Bleeding surgery.

### 11.3 Bleeding aggregator

Add a central bleeding source component:

```csharp
public sealed partial class DwarfBleedingSourcesComponent : Component
{
    [DataField] public float External;
    [DataField] public float Internal;
    [DataField] public float Stump;
}
```

A single system sums these sources and writes
`BloodstreamComponent.BleedAmount` once per tick. `WoundsSystem` should stop
writing `BleedAmount` directly.

## 12. Organs

### 12.1 Component

```csharp
public sealed partial class DwarfOrganHealthComponent : Component
{
    [DataField] public DwarfOrganType Type;
    [DataField] public FixedPoint2 MaxDamage = 100f;
    [DataField] public FixedPoint2 RuptureThreshold = 100f;
    [DataField] public bool Vital;
    [DataField] public bool Ruptured;
}
```

Organ damage uses the organ's `DamageableComponent` or a local damage field.

### 12.2 Damage routing

- When a part takes brute damage, roll for organ damage in that region.
- Armor-piercing and explosions increase the chance.
- Internal bleeding and unsplinted fracture movement can damage organs.
- Extreme toxin damage can damage the liver and kidneys.

### 12.3 Effects

| Organ | Effect |
|---|---|
| Heart | Asphyxiation and bloodloss scale with damage; rupture blocks defibrillation until repaired |
| Lungs | Asphyxiation over time; rupture causes rapid suffocation and blocks anesthetic/internals |
| Kidneys | Poison/Toxin buildup |
| Liver | Poison buildup; slower alcohol metabolism |
| Brain | Blur/stutter/item drop; high damage causes unconsciousness; brain death is unrevivable |
| Eyes | Blur then blindness |
| Stomach | Existing food/drink metabolism; damage reduces nutrition processing |

### 12.4 Defibrillator integration

- Heart rupture or extreme heart damage adds a blocked-defib reason.
- Brain death or a missing brain makes the patient unrevivable.
- Lung damage does not block defib but causes post-revive asphyxiation.
- CPR continues to heal asphyxiation and extend the revive timer.
- Add a heartbreak HUD icon if the HUD is updated.

## 13. Diagnostics

### 13.1 Health scan state

Extend `HealthScanState` with:

```csharp
List<DwarfBodyPartScan> Parts;
List<DwarfOrganScan> Organs;
bool HasInternalBleeding;
```

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
    bool InternalBleeding,
    bool Fractured,
    bool Splinted,
    bool Severed);

public readonly record struct DwarfOrganScan(
    string OrganSlot,
    string OrganName,
    float DamagePercent,
    bool Ruptured,
    BodyPartType Region);
```

Detail levels:

- `HealthAnalyzer`: limb Slash/Brute/Burn accumulators and bleeding.
- `BodyScan`: fractures, internal bleeding, missing limbs.
- `Full`: organ damage.

### 13.2 UI

- Health analyzer shows a compact part list.
- Body scanner shows the full region breakdown.
- Medical records and autodoc data include fractures, internal bleeding, organ
  damage, and missing limbs.
- Examine text can report visible part conditions to medically skilled
  examiners.

## 14. Treatment and surgery

### 14.1 New items

- Splint (`RMCSplint`, `RMCSplint10`).
- Fix-O-Vein (`CMFixOVein`) or a surgical-line substitute.
- Advanced trauma kit and advanced burn kit, if not already implemented.
- Organ repair tool component on the appropriate medical item.
- Stump sealing tools.

### 14.2 New surgeries

| Surgery | Valid when | Effect |
|---|---|---|
| Bone Repair | Part is fractured | Remove fracture |
| Internal Bleeding | Part has internal bleeding | Stop internal bleeding |
| Seal Stump | Part has stump bleeding | Stop stump bleeding |
| Organ Repair | Chest/groin/head has a damaged organ | Repair all damaged organs in the region |
| Amputation | Arm or leg | Sever through the common sever path |
| Limb Reattachment | Parent part with a missing child | Reattach a severed limb held during the step |

### 14.3 Generic surgery helpers

Add a generic part-component condition rather than one condition per condition:

```csharp
public sealed partial class CMSurgeryPartHasComponentConditionComponent : Component
{
    [DataField] public ComponentRegistry Require = new();
    [DataField] public ComponentRegistry Forbid = new();
}
```

Add organ-specific steps and effects under `_DwarfMC/NewMed/Surgery`.

## 15. Wound migration

Wound migration is deferred.

Reason:

- The existing global `WoundedComponent` and `WoundTreater` are already used by
  gauze, ointment, trauma kits, and bleeding.
- NewMed can add part damage, fractures, internal bleeding, and organs without
  moving wound records.
- Migrating wounds to parts changes treatment UX and requires part-targeted
  medical items.
- The body selector already provides part targeting, so migration can happen
  later without redesigning the UI.

v1 behavior:

- Existing global soft-tissue wounds remain.
- Part-local brute/burn damage drives delimbing and fracture thresholds.
- Healing chems heal global damage; the ledger reconciles part damage downward.
- Fractures, internal bleeding, and stumps are NewMed conditions with their own
  treatments.

Future migration:

- Move `Wound` records to `DwarfBodyPartComponent`.
- Make `WoundTreater` use the selected zone.
- Make the health analyzer show per-part wound treatment.
- Keep `WoundedComponent` only as a compatibility aggregate if needed.

## 16. Merge and upstream policy

- All new gameplay code, prototypes, textures, and locale use `_DwarfMC`.
- Do not modify `CMXenoBase` or xeno prototypes for NewMed.
- Do not modify `RMCBaseMobSpeciesOrganic`.
- Keep upstream edits additive and minimal.
- Prefer a partial class or extension system over editing upstream systems.
- Do not move existing RMC14 files into `_DwarfMC`.

## 17. Risks

| Risk | Mitigation |
|---|---|
| Limbs becoming extra HP | Body always takes full damage; limb counters only track delimb progress |
| Delimbing too frequent or too rare | CVars and playtesting |
| Delimbing makes characters unplayable | No extra damage; limb loss is functional, not lethal; limb replacement later |
| Body tree changes break tests | Update Groin mappings and run body integration tests |
| Decapitation transfers mind to brain item | Explicitly block transfer when the head is detached |
| Recursive part removal misses child hands/legs | Remove children through containers so removal events fire |
| Wrapped existing wound system | Keep it for v1; migrate only after part targeting is stable |
| Xenos accidentally affected | Xeno base is separate; add explicit tests |
| Performance from per-hit part lookup | Cache part index per body and update on add/remove |
| Merge conflicts | New files under `_DwarfMC`; keep upstream edits small and documented |

## 18. Tuning assumptions

These are initial assumptions, not final balance:

- The body always takes full damage.
- Limb accumulators receive `LimbDamageShare` of the targeted part's damage.
- Default `LimbDamageShare = 0.5`.
- Default thresholds: Slash 80, Brute 120, Burn 300 for non-head parts.
- Head thresholds are much higher: Slash 300, Brute 500, Burn 1000.
- Delimbing is event-driven. Weapon-specific, source-specific, and scripted
  rules should be added as `DwarfDelimbAttemptEvent` handlers rather than
  hardcoded into the damage routing system.
- Burn delimbing is allowed after death.
- Burn-threshold delimbing ashes the limb even without `OnFireComponent`.
- Fracture chance scales with the hit's slash/brute damage and accumulated
  limb trauma through `FractureScale`.
- `dmc.dwarfmed.fracture_chance_multiplier` scales fracture chance globally.
- `dmc.dwarfmed.internal_bleed_chance_multiplier` and
  `dmc.dwarfmed.internal_bleed_rate_multiplier` scale internal bleeding.
- Stump bleeding rate depends on the part type.
- Field packing slows stump bleeding but only Seal Stump surgery clears it.
- Organ damage chance scales with local damage and armor piercing.
- Selected zone does not change raw body damage.
- `HitWeights` are only used for weighted random selection when no player zone
  is available, and for fallback selection.
- The selector uses a `Base` sprite plus a per-region overlay; opacity tuning is deferred.
- Body healing is normal. Doctor-focused healing also reduces the selected
  part's accumulators.
- If the body is full, limb-only treatment can still reduce accumulators.
- Feature CVar is `dmc.dwarfmed.enabled`.
