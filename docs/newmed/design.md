# NewMed Design Document

Status: Design
Branch: `EvilMed`
Scope: Dwarf-only per-limb medical system

## 1. Summary

NewMed replaces the current body-global medical model with a per-region anatomy
model for dwarfs.

The body is divided into:

- Head
- Chest
- Groin
- Left and right upper arms
- Left and right hands
- Left and right legs
- Left and right feet

Each part tracks local damage. Once a part passes its dismember threshold, slash
attacks have a chance to sever it. Chest and groin cannot be severed. Severing a
limb causes no extra damage, only stump bleeding and loss of function.

The system also adds:

- Fractures
- Internal bleeding
- Internal organs with health and effects
- A bottom-right body-zone selector used for attack targeting and treatment
- New surgical procedures for bones, internal bleeding, stumps, and organs

Existing global health remains the source of truth for critical and death states.
For NewMed dwarfs, the thresholds are 100 critical and 200 dead.

## 2. Goals

- Make limb damage meaningful without requiring a full medical rewrite.
- Make delimbing a tactical consequence rather than a damage spike.
- Keep existing armor, chemistry, defibrillator, and mob-state systems working.
- Keep NewMed dwarf-only and merge-friendly for upstream RMC14 updates.
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

All NewMed code and data lives under the `_DwarfMC` prefix:

```text
Content.Shared/_DwarfMC/NewMed/
  Anatomy/
  Bleeding/
  Fractures/
  Organs/
  Surgery/
  UI/

Content.Server/_DwarfMC/NewMed/
Content.Client/_DwarfMC/NewMed/

Resources/Prototypes/_DwarfMC/NewMed/
  Body/
  Entities/Mobs/Species/
  Organs/
  Surgery/
  Items/

Resources/Textures/_DwarfMC/NewMed/
Resources/Locale/en-US/_DwarfMC/newmed.ftl
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

DwarfNewMedBaseMobSpeciesOrganic (new, abstract, in _DwarfMC)
└── CMMobDwarf (reparented)
```

`DwarfNewMedBaseMobSpeciesOrganic` inherits `RMCBaseMobSpeciesOrganic` and
overrides:

```yaml
- type: entity
  abstract: true
  save: false
  parent: RMCBaseMobSpeciesOrganic
  id: DwarfNewMedBaseMobSpeciesOrganic
  components:
  - type: Body
    prototype: DwarfNewMed
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
  parent: DwarfNewMedBaseMobSpeciesOrganic
  id: CMMobDwarf
```

### 5.3 Xenos are unaffected

Xenos do not inherit `RMCBaseMobSpeciesOrganic`.

`CMXenoBase` is a separate root entity in
`Resources/Prototypes/_RMC14/Entities/Mobs/Xeno/base_xeno.yml` and uses its own
`Body` prototype (`Xenonid`). Reparenting `CMMobDwarf` has no effect on xenos.

No `DwarfMCBaseMobSpeciesXeno` is needed for this feature. A xeno-specific
NewMed base would only be needed if xeno anatomy were brought into scope later.

### 5.4 Upstream edits

The complete list of upstream edits expected for NewMed:

1. `Content.Shared/Body/Part/BodyPartType.cs`: append `Groin`.
2. `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`: handle `Groin`.
3. `Content.Client/_RMC14/Medical/Surgery/CMSurgeryBui.cs`: sort order for `Groin`.
4. `Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.Steps.cs`:
   slot mapping for `Groin`.
5. `Resources/Prototypes/_RMC14/Entities/Mobs/Species/dwarf.yml`: reparent
   `CMMobDwarf` to `DwarfNewMedBaseMobSpeciesOrganic`.

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

The body prototype is `DwarfNewMed`, defined in `_DwarfMC`. It references new
dwarf NewMed part prototypes that inherit the existing dwarf/human sprites and
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

NewMed organ prototypes inherit the existing dwarf/human organ behavior and add
`DwarfOrganHealthComponent`.

## 7. Damage model

### 7.1 Principles

- The mob `DamageableComponent` remains the global health authority.
- Each body part's existing `DamageableComponent` stores local brute and burn
  damage.
- Local damage is routed from the damage event to the selected or weighted part.
- A reconciliation ledger keeps local and global totals consistent.
- Delimbing does not add extra damage.

### 7.2 Ledger

`DwarfAnatomyComponent` on the mob stores:

```csharp
public sealed partial class DwarfAnatomyComponent : Component
{
    [DataField] public bool Enabled = true;

    // Damage not attributable to an attached part.
    [DataField] public FixedPoint2 SystemicBrute;
    [DataField] public FixedPoint2 SystemicBurn;

    // Damage that left the body with a severed limb.
    [DataField] public FixedPoint2 LostLimbBrute;
    [DataField] public FixedPoint2 LostLimbBurn;

    [DataField] public TimeSpan ReconcileInterval = TimeSpan.FromSeconds(1);
    [DataField] public TimeSpan ReconcileAt;

    [DataField] public bool AllowDelimbing = true;
    [DataField] public float DelimbScale = 0.75f;
    [DataField] public float DelimbMaxChance = 0.75f;

    [DataField] public Dictionary<BodyPartType, float> HitWeights = new()
    {
        [BodyPartType.Head] = 10f,
        [BodyPartType.Torso] = 30f,
        [BodyPartType.Groin] = 10f,
        [BodyPartType.Arm] = 16f,   // total for both arms
        [BodyPartType.Hand] = 8f,   // total for both hands
        [BodyPartType.Leg] = 20f,   // total for both legs
        [BodyPartType.Foot] = 8f,   // total for both feet
    };
}
```

Invariant:

```text
globalBrute = sum(attached part brute) + SystemicBrute + LostLimbBrute
globalBurn  = sum(attached part burn)  + SystemicBurn  + LostLimbBurn
```

On damage increase:

- The normal damage pipeline already increases global damage.
- `DwarfBodyPartDamageSystem` selects a part and adds the same brute/burn delta
  to that part.
- Both sides of the ledger increase equally.

On healing:

- The global damage decreases.
- Proactive healing reduces part damage proportionally.
- The reconciliation pass is the safety net.

On severing:

- Move the severed part's local damage into `LostLimb*`.
- Global health does not change.

On reattachment (future):

- Move the part's damage from `LostLimb*` back into the part.
- Global health does not change.

### 7.3 Part thresholds

`DwarfBodyPartComponent` on each part stores:

```csharp
public sealed partial class DwarfBodyPartComponent : Component
{
    [DataField] public bool Dismemberable = true;
    [DataField] public FixedPoint2 DismemberThreshold = 40f;
    [DataField] public FixedPoint2 FractureThreshold = 20f;
    [DataField] public FixedPoint2 OrganDamageThreshold = 25f;
    [DataField] public float BleedMultiplier = 1f;
    [DataField] public float FractureChanceMultiplier = 1f;
    [DataField] public float InternalBleedChanceMultiplier = 1f;
}
```

Initial values:

| Part | Dismember X | Fracture at | Dismemberable |
|---|---:|---:|---|
| Head | 50 | 25 | Yes |
| Chest | 100 | 50 | No |
| Groin | 80 | 40 | No |
| Arm | 40 | 20 | Yes |
| Hand | 25 | 12 | Yes |
| Leg | 40 | 20 | Yes |
| Foot | 25 | 12 | Yes |

These values are starting points and are expected to change after playtesting.

### 7.4 Delimbing roll

Only `Slash` damage can delimb.

```text
if part is not dismemberable: do not roll
if local slash damage < part.DismemberThreshold: do not roll
over = local damage - part.DismemberThreshold
chance = min(DelimbMaxChance, (over / part.DismemberThreshold) * DelimbScale)
```

With the defaults:

- At `X + 0.5X`, chance is about 37.5%.
- At `2X`, chance is 75%.

The roll happens per slash attack, after local damage is applied. A failed roll
does not damage the limb further beyond the attack's normal damage.

### 7.5 Global thresholds

NewMed dwarfs use:

```yaml
MobThresholds:
  thresholds:
    0: Alive
    100: Critical
    200: Dead
```

Damage to any part contributes to global damage, so a character can enter
critical or die before a limb is severed.

## 8. Hit selection overlay

### 8.1 Purpose

One selector is used for both:

1. The zone the player's attacks target.
2. The zone a medic treats.

Raw damage is unchanged by zone selection in v1. The zone only determines where
local damage, fractures, delimbing attempts, and organ damage land.

### 8.2 Client UI

- New `DwarfBodyZoneSelectorControl`.
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
- Selected region is highlighted.
- Missing parts are greyed out and disabled, with a fallback to the parent part.
- Toggle keybind/action; optionally auto-show while holding a medical or surgical
  item.
- Optional later: injury icons for fracture, bleeding, and organ damage.

### 8.3 Server state

`DwarfBodyZoneTargetComponent` on the player stores the selected `DwarfBodyZone`.

- Client sends a selection event.
- Server validates the player is alive and NewMed-enabled.
- Selection is replicated to the client.
- If the selected part is missing, the server falls back to the parent part, then
  to a weighted random attached part.

### 8.4 Attack routing

`DamageChangedEvent` carries `Origin`.

- If `Origin` has `DwarfBodyZoneTargetComponent`, use that zone.
- Projectiles use `ProjectileComponent.Shooter` as `Origin`, so guns work.
- NPC and xeno attacks have no selector and use weighted random selection.
- Special attacks can later provide an explicit zone, such as headbite.

### 8.5 Treatment targeting

Medical items and splints use the user's selected zone.

- If the user has no selection, choose the most damaged valid part.
- Surgery continues to use the existing surgery UI part selection.

## 9. Delimbing

### 9.1 Common sever path

`DwarfSeverSystem.TrySeverPart` performs:

1. Raise a cancellable `BeforeBodyPartSeveredEvent`.
2. Recursively remove child parts through their containers, so
   `BodyPartRemovedEvent` fires for hands, feet, and legs.
3. Detach the limb subtree and drop it as an item.
4. Move the part's local damage into `LostLimb*`.
5. Add stump bleeding to the parent part or body.
6. Add `SeveredLimbComponent` to the dropped limb.
7. Play audio, popups, and blood effects.

The existing `HandsSystem` removes hand slots when hand parts are removed. The
existing `BodySystem` hides humanoid sprite layers when parts are removed.

### 9.2 Per-part behavior

| Part | Behavior |
|---|---|
| Head | Decapitation; death; unrevivable; block brain-to-mind transfer into the severed head |
| Chest | Cannot be severed |
| Groin | Cannot be severed |
| Arm | Hand detaches with the arm; hand slot and sprite removed; heavy stump bleed |
| Hand | Hand slot and sprite removed |
| Leg | Foot detaches with the leg; movement penalty; knockdown if both legs gone |
| Foot | Movement penalty |

### 9.3 Stump bleeding

Stump bleeding is a bleeding source, not instant damage.

- It is treated by the Seal Stump surgery.
- It continues until sealed.
- It can cause bloodloss critical/death if ignored.

### 9.4 Decapitation

Decapitation is the only severing case that directly causes death.

- Mark the body dead and unrevivable.
- Prevent the brain from transferring the player's mind into the severed head.
- Synthetic head reattachment is a future exception.

## 10. Fractures

### 10.1 Trigger

A fracture rolls when local brute damage crosses the part's fracture threshold.
Chance scales with damage past the threshold.

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
    FixedPoint2 Brute,
    FixedPoint2 Burn,
    bool Bleeding,
    bool InternalBleeding,
    bool Fractured,
    bool Splinted,
    bool Severed,
    float HealthPercent);

public readonly record struct DwarfOrganScan(
    string OrganSlot,
    string OrganName,
    float DamagePercent,
    bool Ruptured,
    BodyPartType Region);
```

Detail levels:

- `HealthAnalyzer`: part brute/burn and bleeding.
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
| Limb Replacement | Parent part with a missing child | Reattach a limb (future phase) |

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
| Global/local damage drift | Ledger invariant and 1s reconcile pass |
| Delimbing too frequent or too rare | CVars and playtesting |
| Delimbing makes characters unplayable | No extra damage; limb loss is functional, not lethal; limb replacement later |
| Body tree changes break tests | Update Groin mappings and run body integration tests |
| Decapitation transfers mind to brain item | Explicitly block transfer when the head is detached |
| Recursive part removal misses child hands/legs | Remove children through containers so removal events fire |
| Wrapped existing wound system | Keep it for v1; migrate only after part targeting is stable |
| Xenos accidentally affected | Xeno base is separate; add explicit tests |
| Performance from per-hit part lookup | Cache part index per body and update on add/remove |
| Merge conflicts | New files under `_DwarfMC`; only five small upstream edits |

## 18. Tuning assumptions

These are initial assumptions, not final balance:

- Delimb threshold values in section 7.3.
- `DelimbScale = 0.75`, `DelimbMaxChance = 0.75`.
- Fracture chance scales with damage past the fracture threshold.
- Stump bleeding rate depends on the part type.
- Organ damage chance scales with local damage and armor piercing.
- Selected zone does not change raw damage.
- Treatment uses the same selected zone.
