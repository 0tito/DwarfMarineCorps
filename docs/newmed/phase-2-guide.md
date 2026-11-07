# Phase 2 Implementation Guide

Phase: Fractures, splints, internal bleeding, and treatment
Status: Next
Branch: `EvilMed`
Related design: [design.md](design.md)
Related plan: [implementation-plan.md](implementation-plan.md)
Current implementation map: [current-state.md](current-state.md)

## Goal

Add:

- Fractures from brute damage.
- Splints.
- Fracture movement and item-drop effects.
- Internal bleeding from fractures and severe brute damage.
- Bone Repair and Internal Bleeding surgery.
- Scanner/analyzer flags for fracture, splint, and internal bleeding.
- Tests for the full fracture/internal-bleeding/treatment loop.

Organ damage effects remain Phase 3. Phase 2 may record organ-damage risk, but
actual organ health/effects are not part of this phase.

## Existing files to read first

```text
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfBodyPartComponent.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfBodyPartConditionComponent.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfBodyPartScan.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfBodyPartScanSystem.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfMovementSystem.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfLimbDamage.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfLimbTreatmentDoAfterEvent.cs
Content.Server/_DwarfMC/Medical/DwarfMed/DwarfBodyPartDamageSystem.cs
Content.Server/_DwarfMC/Medical/DwarfMed/DwarfBleedingSystem.cs
Content.Server/_DwarfMC/Medical/DwarfMed/DwarfLimbTreatmentSystem.cs
```

Surgery anchors:

```text
Content.Server/_RMC14/Medical/Surgery/CMSurgerySystem.cs
Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.cs
Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.Steps.cs
Resources/Prototypes/_RMC14/Entities/Surgery/cm_surgery_steps.yml
Resources/Prototypes/_RMC14/Entities/Surgery/cm_surgeries.yml
Resources/Prototypes/_RMC14/Entities/Objects/Medical/surgery.yml
```

Existing surgery tools:

```text
CMBoneGel
CMBoneSetter
CMBoneSaw
CMScalpel
CMHemostat
CMRetractor
CMCautery
CMSurgicalLine
```

## 1. Data and components

`DwarfBodyPartConditionComponent` already has:

```csharp
public bool Fractured;
public bool Splinted;
public bool InternalBleeding;
public float InternalBleedRate;
```

`DwarfBodyPartComponent` already has:

```csharp
public FixedPoint2 FractureThreshold;
public FixedPoint2 OrganDamageThreshold;
public float FractureChanceMultiplier;
public float InternalBleedChanceMultiplier;
```

Add if needed:

```csharp
[DataField] public FixedPoint2 InternalBleedRate = 0.2f;
```

## 2. Fracture system

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfFractureSystem.cs`

Trigger it from `DwarfBodyPartDamageSystem` after `BruteDamage` is increased.

Rules:

- Only brute damage fractures parts.
- Skip if already fractured.
- Require `BruteDamage >= FractureThreshold`.
- Chance scales with damage past the threshold:

```text
over = BruteDamage - FractureThreshold
chance = clamp(over / FractureThreshold, 0, 1) * FractureChanceMultiplier
```

- On success:
  - Set `Fractured = true`.
  - Play a bone/crack sound.
  - Show a popup.
  - Dirty the part.

Fractures persist until surgery. Healing does not remove them.

## 3. Splints

Create:

```text
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfSplintComponent.cs
Content.Shared/_DwarfMC/Medical/DwarfMed/DwarfSplintDoAfterEvent.cs
Content.Server/_DwarfMC/Medical/DwarfMed/DwarfSplintSystem.cs
Resources/Prototypes/_DwarfMC/Medical/Items/Splint.yml
```

Behavior:

- Use on a patient with a selected body zone.
- Requires the selected part to be fractured and not already splinted.
- Uses a do-after.
- Sets `Splinted = true`.
- Consumes one splint.
- If the body part is missing, fall back through `BodyZoneSystem`.
- Splinted blocks:
  - item-drop effects
  - most movement penalty
  - fracture-movement internal bleeding risk
- Splinting does not heal the fracture.

Suggested item IDs:

```text
RMCSplint
RMCSplint10
```

## 4. Fracture effects

### Arm / hand fractures

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfFractureEffectSystem.cs`

Hook `UseInHandEvent` and/or attack attempts.

- If the active arm/hand is fractured and not splinted:
  - Roll a chance to drop the held item.
  - Use `SharedHandsSystem.TryDrop`.
- Splinted fractures do not drop items.

### Leg / foot fractures

Extend `DwarfMovementSystem`.

Suggested values:

```text
Unsplinted leg/foot fracture: 0.65x
Splinted leg/foot fracture:   0.9x
```

Do not double-penalize missing feet/legs.

### Chest / groin / head fractures

For Phase 2:

- Mark the fracture.
- Add internal-bleeding risk.
- Do not implement organ damage yet. That is Phase 3.

## 5. Internal bleeding

Extend `DwarfBleedingSystem`:

```text
if StumpBleeding: args.Amount += StumpBleedRate
if InternalBleeding: args.Amount += InternalBleedRate
```

Create:

`Content.Server/_DwarfMC/Medical/DwarfMed/DwarfInternalBleedingSystem.cs`

Causes:

- Unsplinted fracture movement.
- Severe chest/groin/head brute damage.
- Optional armor-piercing routing later; the current `DamageChangedEvent`
  does not expose armor-piercing cleanly.

Movement check:

- On a 1-second cooldown while moving.
- For each fractured and unsplinted part in a risky region:
  - Roll `InternalBleedChanceMultiplier`.
  - Set `InternalBleeding = true`.
  - Set `InternalBleedRate`.
  - Dirty the part.

Internal bleeding continues until surgery.

## 6. Surgery

### Conditions

Add shared components:

```text
DwarfSurgeryFracturedConditionComponent
DwarfSurgeryInternalBleedingConditionComponent
```

Subscribe them to `CMSurgeryValidEvent`.

### Effects

Add shared components:

```text
DwarfSurgeryStepRepairFractureEffectComponent
DwarfSurgeryStepStopInternalBleedingEffectComponent
```

Repair fracture effect:

- Set `Fractured = false`.
- Set `Splinted = false`.
- Dirty the part.

Stop internal bleeding effect:

- Set `InternalBleeding = false`.
- Set `InternalBleedRate = 0`.
- Dirty the part.

### Bone Repair surgery

Valid when the part is fractured.

Steps:

1. Open incision.
2. Bone Gel + Bone Setter.
3. Close incision.

### Internal Bleeding surgery

Valid when the part has internal bleeding.

Steps:

1. Open incision.
2. Fix-O-Vein or Surgical Line.
3. Close incision.

### Fix-O-Vein

Prefer a `_DwarfMC` item that inherits `CMSurgicalLine` and adds a new
`CMFixOVeinComponent` so upstream surgery items stay untouched:

`Resources/Prototypes/_DwarfMC/Medical/Items/FixOVein.yml`

If that is too much for Phase 2, add `CMFixOVeinComponent` to `CMSurgicalLine`
with a small upstream edit.

### YAML paths

```text
Resources/Prototypes/_DwarfMC/Medical/Surgery/steps.yml
Resources/Prototypes/_DwarfMC/Medical/Surgery/surgeries.yml
```

## 7. Diagnostics

Extend `DwarfBodyPartScan` with:

```csharp
bool Fractured;
bool Splinted;
bool InternalBleeding;
```

Update:

- `DwarfBodyPartScanSystem`
- health analyzer debug rows
- scanner/records if desired

Example:

```text
Left arm   Slash 20/80  Brute 45/120  FRACTURED
Right leg  Slash 0/80   Brute 30/120  SPLINTED
Groin      Brute 85/120 INTERNAL BLEEDING
```

## 8. Tests

Create:

```text
Content.IntegrationTests/_DwarfMC/DwarfMed/DwarfMedFractureTest.cs
Content.IntegrationTests/_DwarfMC/DwarfMed/DwarfMedBleedingTest.cs
Content.IntegrationTests/_DwarfMC/DwarfMed/DwarfMedSurgeryTest.cs
```

Cases:

- Fracture triggers when brute crosses threshold.
- Fracture does not trigger before threshold.
- Splint applies `Splinted`.
- Splinted blocks item-drop effect.
- Splinted blocks most movement penalty.
- Unsplinted fracture movement causes internal bleeding.
- Internal bleeding increases `DwarfBleedingEvent` amount.
- Bone Repair surgery clears fracture.
- Internal Bleeding surgery clears internal bleeding.
- Scanner reports fracture/splint/internal bleeding.
- Splint is consumed.
- Fracture does not heal from normal body healing.

## 9. Upstream edits

Phase 2 should need no new upstream edits beyond Phase 1, unless the Fix-O-Vein
approach is added directly to `CMSurgicalLine`.

All new systems, conditions, effects, items, and prototypes should go under
`_DwarfMC`.

## 10. Implementation order

1. Add `DwarfFractureSystem` and fracture roll.
2. Add fracture flags to scanner data.
3. Add splint item and `DwarfSplintSystem`.
4. Extend `DwarfMovementSystem` with fracture penalties.
5. Add arm/hand item-drop effects.
6. Add internal bleeding movement checks and extend `DwarfBleedingSystem`.
7. Add surgery conditions/effects.
8. Add Bone Repair and Internal Bleeding YAML.
9. Add tests.
10. Verify in-game.

## 11. Acceptance criteria

- Build passes.
- All existing DwarfMed tests still pass.
- Fractures trigger at the configured threshold.
- Splints apply and block negative effects.
- Unsplinted movement can cause internal bleeding.
- Internal bleeding contributes to bloodloss.
- Bone Repair surgery clears fractures.
- Internal Bleeding surgery clears IB.
- Scanner reports fracture/splint/IB.
- No Phase 1 behavior regresses.

## 12. Decisions

These are the current assumptions:

- Fractures are chance-based, not guaranteed at threshold.
- Splinted fractures still cause a small slowdown.
- Fractures only heal by surgery.
- Organ damage from chest/groin/head fractures is deferred to Phase 3.
- Armor-piercing internal bleeding is deferred until a clean AP path exists.
- Splints are consumed on use.
- Fix-O-Vein should live under `_DwarfMC` if practical.
