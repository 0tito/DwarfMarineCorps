# DwarfMed Implementation Plan

Status: Phase 0, Phase 1, and Phase 2 complete; Phase 3 next
Branch: `EvilMed`
Related design: [design.md](design.md)
Current implementation map: [current-state.md](current-state.md)
Phase 1 implementation guide: [phase-1-guide.md](phase-1-guide.md)
Phase 2 implementation guide: [phase-2-guide.md](phase-2-guide.md)

Naming note: `NewMed` is the design name. The implementation uses `DwarfMed`,
`DwarfMedBaseMobSpeciesOrganic`, and the body prototype `DMCDwarf`.

## 1. Working rules

- Keep new gameplay code and data under `_DwarfMC`.
- Keep upstream edits small, additive, and documented.
- Do not touch `RMCBaseMobSpeciesOrganic` or `CMXenoBase`.
- Keep the feature behind `dmc.dwarfmed.enabled`.
- Add tests with each phase, not only at the end.
- Do not commit unless explicitly requested.

## 2. Current file layout

See [current-state.md](current-state.md) for the complete actual file map and
test status.

Key prefixes:

```text
Content.Shared/_DwarfMC/Medical/DwarfMed/
Content.Server/_DwarfMC/Medical/DwarfMed/
Content.Client/_DwarfMC/Medical/UI/
Content.IntegrationTests/_DwarfMC/DwarfMed/
Resources/Prototypes/_DwarfMC/
Resources/Textures/_DwarfMC/Interface/Medical/BodySelection/
docs/newmed/
```

Phase 2 should extend these prefixes, not restructure them.

## 3. Upstream edits

Current and planned upstream edits:

1. `Content.Shared/Body/Part/BodyPartType.cs`
   - Append `Groin`.
2. `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`
   - Handle `Groin`.
   - Add self-sublayers for `LHand`, `RHand`, `LFoot`, and `RFoot`.
3. `Content.Client/_RMC14/Medical/Surgery/CMSurgeryBui.cs`
   - Sort order for `Groin`.
4. `Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.Steps.cs`
   - Groin slot mapping.
5. `Resources/Prototypes/_RMC14/Entities/Mobs/Species/dwarf.yml`
   - Reparent `CMMobDwarf` to `DwarfMedBaseMobSpeciesOrganic`.
6. Phase 1: `Content.Server/_RMC14/Medical/Wounds/WoundsSystem.cs`
   - Raise `DwarfBleedingEvent`.
   - Include the event amount in bloodloss.
   - Keep `WoundedComponent` while NewMed bleeding is active.
7. Phase 1: `Content.Server/Body/Systems/BrainSystem.cs`
   - Skip mind transfer when the brain's parent body part is detached.
8. Phase 1 scanner/records integration:
   - `Content.Shared/_RMC14/Medical/Scanner/HealthScannerUI.cs`
   - `Content.Shared/_RMC14/Medical/Scanner/HealthScannerSystem.cs`
   - `Content.Server/_RMC14/RMCMedicalRecords/RMCMedicalRecordsSystem.cs`
   - `Content.Client/_RMC14/Medical/Scanner/HealthScannerUiData.cs`
   - `Content.Client/_RMC14/Medical/Scanner/HealthScannerWindow.xaml`

If an edit can be replaced by a `_DwarfMC` partial class or public API, do that
instead.

## 4. Phase 0 - Complete

### Completed

- Added `_DwarfMC` code, prototype, locale, and test folders.
- Added `Groin` to `BodyPartType` and the required switch mappings.
- Added `BaseGroin` to `Resources/Prototypes/Body/Parts/base.yml`.
- Added the `DMCDwarf` body prototype with chest, groin, arms, hands, legs, and
  feet.
- Added all 11 `DwarfMed*` part prototypes.
- Added all 7 required `DwarfMed*` organ prototypes.
- Added `DwarfMedBaseMobSpeciesOrganic` and reparented `CMMobDwarf`.
- Added the Phase 0 components:
  - `DwarfAnatomyComponent`
  - `DwarfBodyPartComponent`
  - `DwarfBodyPartConditionComponent`
  - `DwarfOrganHealthComponent`
  - `BodyZone`
  - `BodyZoneTargetComponent`
- Added `dmc.dwarfmed.enabled`.
- Added the `dwarfmedbody` debug command.
- Added `DwarfMedBodyTest`.
- Build passes.
- `DwarfMedBodyTest` passes: 2/2.

### Phase 0 verification

- `CMMobDwarf` uses `DMCDwarf`.
- Body has 11 parts and 7 organs.
- Dwarf thresholds are 100 critical / 200 dead.
- `CMMobHuman` and `CMXenoLarva` are unaffected.
- Xenos do not have `DwarfAnatomyComponent`.

## 5. Phase 1 - Body-zone damage, selection, and delimbing

Phase 1a and 1b are merged. Phase 1 includes the selection UI and the first
limb-treatment hook.

See [phase-1-guide.md](phase-1-guide.md) for the step-by-step implementation
guide.

### Outcome

- The body always takes full damage.
- The targeted part receives `LimbDamageShare` of that damage as local delimb
  progress.
- Limbs are not extra HP and do not make marines tankier.
- The bottom-right selector is available for dwarfs immediately.
- Selected zone is replicated to the server.
- Player attacks use the selected zone, including player-controlled xenos.
- NPC attacks use weighted random selection.
- Limb accumulators track Slash, Brute, and Burn separately.
- Delimbing is event-driven:
  - `DwarfDelimbAttemptEvent` when an accumulator crosses its threshold.
  - `DwarfSeverRequestEvent` for guaranteed severing.
- Chest and groin never delimb.
- Delimbing adds no direct damage, only stump bleeding and loss of function.
- Hands lose hand slots; feet/legs apply movement penalties.
- Head delimbing kills and prevents mind transfer into the severed head.
- Doctor-focused healing reduces the selected part's accumulators.
- Limb-only treatment is allowed even when the body is at full health.
- Health analyzer shows per-part accumulators and missing limbs.

**Status: Complete. 11/11 DwarfMed tests passing.**

### Implemented in Phase 1

- `LimbDamageShare` and `HitWeights` on `DwarfAnatomyComponent`.
- Limb accumulators, thresholds, `MaxLocalDamage`, and `StumpBleedRate` on
  `DwarfBodyPartComponent`.
- `DwarfBleedingEvent`, `DwarfDelimbAttemptEvent`, `DwarfSeverRequestEvent`,
  and `DwarfSeverReason`.
- `BodyZoneSelectedEvent`.
- `BodyZoneTargetSystem` using `PlayerAttachedEvent`/`PlayerDetachedEvent` so all
  player-controlled mobs, including xenos, get `BodyZoneTargetComponent`.
- Server systems:
  - `DwarfBodyPartDamageSystem`
  - `DwarfDelimbPolicySystem`
  - `DwarfSeverSystem`
  - `DwarfBleedingSystem`
  - `BodyZoneTargetSystem`
  - `DwarfLimbTreatmentSystem`
  - `DwarfMedSetupSystem`
- Shared systems:
  - `DwarfMovementSystem`
  - `SharedBodySystem.DwarfSever.cs`
- Existing client UI wired:
  - `DwarfMedicalBodyOverlay`
  - `DwarfMedicalBodyOverlayUIController`
- Existing textures used:
  - `Resources/Textures/_DwarfMC/Interface/Medical/BodySelection/`
- `HealthScanState` and health analyzer debug readout extended with limb
  accumulators.
- Phase 1 tests added.
- Severed limbs are thrown randomly with a delimb sound.
- Arms drop hands separately; legs drop feet separately.
- Fire applies heat to every limb and turns fire-delimbed limbs into `Ash`.
- `MaxDamageComponent` removed for DwarfMed dwarfs by `DwarfMedSetupSystem`.

### Phase 1 acceptance criteria

- Build passes.
- Dwarf body and existing Phase 0 tests still pass.
- Selected zone routes limb share to the correct part.
- Weighted random selection uses `HitWeights` for NPC attacks and fallback.
- `HitWeights` are not used when a specific zone is selected.
- Body damage is never reduced by limb accumulators.
- Slash/brute/burn accumulators are tracked separately.
- Threshold crossing raises the delimb event.
- Hand and arm severing remove the hand slot.
- Foot loss reduces movement speed.
- Chest and groin never sever.
- Delimbing adds no direct global damage.
- Head severing kills and makes the patient unrevivable.
- Doctor-focused healing reduces the selected limb accumulators.
- Limb-only healing works when the body is at full health.
- Health analyzer shows part accumulators and missing limbs.
- Non-dwarf targets are unaffected by DwarfMed anatomy/damage routing; player
  xenos still receive the selector.

## 6. Phase 2 - Complete

See [phase-2-guide.md](phase-2-guide.md) for the detailed implementation guide.

**Status: Complete. 38/38 DwarfMed tests passing.**

### Implemented in Phase 2

- Chance-based fractures from slash and brute limb damage.
- `FractureScale` replaces the old hard fracture threshold.
- Global CVar multipliers for fracture and internal-bleed chances.
- Splint item, splint application, removal verb, and splint consumption.
- Splinted fractures block item-drop effects and reduce movement penalties.
- Internal bleeding from severe head/chest/groin brute damage and unsplinted
  fracture movement.
- Internal bleeding contributes to `DwarfBleedingEvent` and keeps
  `WoundedComponent` alive.
- Stump bleeding field stopgap: gauze/trauma kits reduce the rate but do not
  clear it.
- Bone Repair, Internal Bleeding, Seal Stump, and Limb Reattachment surgeries.
- `RMCFixOVein` and `CMFixOVein`.
- Scanner, health analyzer debug, and `dwarfmedbody` condition flags.
- Burn-threshold delimbing ashes the limb even without `OnFireComponent`.
- Direct WoundTreater use starts limb treatment when the selected zone has
  matching limb damage.
- Open incisions add a slow bleed and keep the surgery listed until closed.
- Dead dwarfs continue DwarfMed bleeding at half rate.
- Bone Repair uses separate bone gel and bone setter steps; the gel marker is
  cleared by a DwarfMed close-incision step.
- Limb Reattachment reattaches dropped `DwarfSeveredLimb` items to matching
  missing child slots and stays available until the incision is closed.
- `DwarfMedDebugCrate` with debug damage weapons, instant delimb/fracture/bleed
  sticks, splints, healing items, and surgery tools.

### Phase 2 acceptance criteria

- Build passes.
- All existing DwarfMed tests still pass.
- Fractures trigger from slash and brute, never burn.
- Fracture chance scales with dealt damage and accumulated limb trauma.
- Splints apply, block item-drop effects, and reduce movement penalties.
- Unsplinted movement can cause internal bleeding.
- Internal bleeding contributes to bloodloss.
- Field packing slows but does not clear stump bleeding.
- Bone Repair clears fractures.
- Internal Bleeding clears internal bleeding.
- Seal Stump clears stump bleeding.
- Scanner reports fracture/splint/internal bleeding/stump.
- No Phase 1 behavior regresses.

## 7. Phase 3 - Organs (next)

- Organ damage routing and effects.
- Organ repair surgery.
- Heart, lung, kidney, liver, brain, and eye effects.
- Defibrillator/CPR/unrevivable integration.
- Scanner, records, and autodoc updates.

## 8. Phase 4 - Integration and polish

- Body selector injury icons.
- Guidebook and locale.
- Autodoc procedures and research upgrades.
- Medical records and body scanner output.
- Admin tools, VV, and rejuvenate handling.
- Save/load and performance passes.
- Balance pass.

## 9. Phase 5 - Optional

- Pain system.
- Limb reattachment and prosthetics.
- Limb-specific wound migration.
- Shrapnel and foreign objects.
- Eschar and severe burns.
- Infections.
- Per-zone damage multipliers.

## 10. Test matrix

| Area | Test |
|---|---|
| Scope | Dwarf gets DwarfMed anatomy; all player mobs get the body-zone selector |
| Body | Groin, hands, legs, feet exist and behave |
| Thresholds | Dwarf crits at 100, dies at 200 |
| Part damage | Damage lands on selected/random part |
| Body-first damage | Body always takes full damage; limb counters never reduce it |
| Hit weights | Weighted random is used for NPC attacks and fallback; selected zone overrides weights |
| Delimb | Accumulator below threshold never severs; threshold crossing uses the event pipeline |
| Accumulators | Slash, Brute, and Burn track separately |
| Healing | Doctor-focused healing reduces selected limb accumulators; limb-only healing works at full body health |
| Delimb immunity | Chest and groin never sever |
| Delimb cost | No extra direct damage from severing |
| Hands | Hand slot removed on hand/arm sever |
| Movement | Foot/leg loss slows movement |
| Head | Decapitation kills and prevents mind transfer |
| Selector | Selection replicates and routes attacks |
| Stump bleeding | Severed limb adds a bleeding source |
| Stump stopgap | Gauze/trauma kit packs reduce but do not clear stump bleeding |
| Fractures | Slash and brute cause chance-based fractures; burn does not |
| Splints | Splints apply, consume, remove, and block fracture effects |
| Internal bleeding | Unsplinted movement can start internal bleeding |
| Surgery | Bone Repair, Internal Bleeding, and Seal Stump clear their conditions |
| Scanner | Analyzer shows part damage, conditions, and missing limbs |
| Isolation | Non-dwarf targets are unaffected by DwarfMed damage routing; player xenos still get the selector |

## 11. Verification commands

```bash
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-restore /m
```

```bash
dotnet test --no-build --configuration DebugOpt \
  Content.IntegrationTests/Content.IntegrationTests.csproj \
  --filter "FullyQualifiedName~DwarfMed" \
  -- NUnit.ConsoleOut=0
```

The filter must come before `--`. If it comes after `--`, VSTest runs the whole
suite and the run can take a long time.

## 12. Rollout

- Keep `dmc.dwarfmed.enabled` enabled during EvilMed development.
- Guard Phase 1 systems with the CVar.
- Keep global wounds in place until the part damage and treatment paths are
  stable.
- Do not enable DwarfMed on any non-dwarf species.

## 13. Branch policy

All DwarfMed work goes on the `EvilMed` branch. Do not commit unless explicitly
requested.
