# NewMed Implementation Plan

Status: Ready for Phase 0
Branch: `EvilMed`
Related design: [design.md](design.md)

## 1. Working rules

- Keep all new code and data under `_DwarfMC`.
- Keep the five upstream edits listed in the design document minimal.
- Do not touch `RMCBaseMobSpeciesOrganic` or `CMXenoBase`.
- Keep the feature behind a CVar during development.
- Add tests with each phase, not only at the end.
- Do not commit unless explicitly requested.

## 2. Final file layout

```text
Content.Shared/_DwarfMC/NewMed/
  Anatomy/
    DwarfAnatomyComponent.cs
    DwarfBodyPartComponent.cs
    DwarfBodyPartConditionComponent.cs
    DwarfBodyPartDamageSystem.cs
    DwarfBodyPartReconcileSystem.cs
    DwarfSeverSystem.cs
    DwarfBodyZone.cs
    DwarfBodyZoneTargetComponent.cs
    DwarfBodyZoneTargetSystem.cs
    DwarfBodyPartScan.cs
  Bleeding/
    DwarfBleedingSourcesComponent.cs
    DwarfBleedingSystem.cs
  Fractures/
    DwarfFractureSystem.cs
    DwarfSplintComponent.cs
    DwarfSplintSystem.cs
  Organs/
    DwarfOrganHealthComponent.cs
    DwarfOrganSystem.cs
  Surgery/
    DwarfSurgeryEffects.cs
    DwarfSurgeryConditions.cs
  UI/
    DwarfBodyZoneSelectorControl.cs
    DwarfBodyZoneSelectorUIController.cs

Content.Server/_DwarfMC/NewMed/
  DwarfNewMedServerSystem.cs
  DwarfBodyPartDamageServerSystem.cs
  DwarfOrganServerSystem.cs
  DwarfBleedingServerSystem.cs
  DwarfSurgeryServerSystem.cs

Content.Client/_DwarfMC/NewMed/
  DwarfBodyZoneSelectorSystem.cs
  DwarfBodyZoneSelectorWindow.cs
  DwarfBodyZoneSelectorWindow.xaml

Resources/Prototypes/_DwarfMC/NewMed/
  Body/body_dwarf_newmed.yml
  Body/parts.yml
  Organs/organs.yml
  Entities/Mobs/Species/dwarf_base.yml
  Surgery/steps.yml
  Surgery/surgeries.yml
  Items/splint.yml
  Items/fix_o_vein.yml

Resources/Textures/_DwarfMC/NewMed/
  body_zone_selector.rsi/

Resources/Locale/en-US/_DwarfMC/newmed.ftl
docs/newmed/
```

## 3. Upstream edits

These are the only expected upstream changes:

1. `Content.Shared/Body/Part/BodyPartType.cs`
   - Append `Groin` to the enum.
2. `Content.Shared/Humanoid/HumanoidVisualLayersExtension.cs`
   - Add `BodyPartType.Groin`.
   - Return no visual layer for Groin.
3. `Content.Client/_RMC14/Medical/Surgery/CMSurgeryBui.cs`
   - Add a sort order for Groin.
4. `Content.Shared/_RMC14/Medical/Surgery/SharedCMSurgerySystem.Steps.cs`
   - Map Groin to `SlotFlags.OUTERCLOTHING | SlotFlags.LEGS`.
5. `Resources/Prototypes/_RMC14/Entities/Mobs/Species/dwarf.yml`
   - Reparent `CMMobDwarf` to `DwarfNewMedBaseMobSpeciesOrganic`.

If a fix can be implemented in a `_DwarfMC` partial class or extension instead,
do that and avoid an upstream edit.

## 4. Phase 0 - Scaffolding and wiring

### Deliverables

- Create `docs/newmed/` documents.
- Add `_DwarfMC` folders.
- Add `DwarfNewMed` body prototype.
- Add NewMed part and organ prototypes.
- Add `DwarfNewMedBaseMobSpeciesOrganic`.
- Reparent `CMMobDwarf`.
- Add `BodyPartType.Groin` and required mapping edits.
- Add base components with no gameplay effects yet:
  - `DwarfAnatomyComponent`
  - `DwarfBodyPartComponent`
  - `DwarfBodyPartConditionComponent`
  - `DwarfOrganHealthComponent`
  - `DwarfBodyZoneTargetComponent`
- Add CVar `dwarf.newmed.enabled`, defaulting to true on the EvilMed branch.
- Add an admin command to print the dwarf body tree for verification.

### Files

- `Resources/Prototypes/_DwarfMC/NewMed/Entities/Mobs/Species/dwarf_base.yml`
- `Resources/Prototypes/_DwarfMC/NewMed/Body/body_dwarf_newmed.yml`
- `Resources/Prototypes/_DwarfMC/NewMed/Body/parts.yml`
- `Resources/Prototypes/_DwarfMC/NewMed/Organs/organs.yml`
- `Content.Shared/_DwarfMC/NewMed/Anatomy/DwarfAnatomyComponent.cs`
- `Content.Shared/_DwarfMC/NewMed/Anatomy/DwarfBodyPartComponent.cs`
- `Content.Shared/_DwarfMC/NewMed/Organs/DwarfOrganHealthComponent.cs`

### Acceptance criteria

- The project builds.
- `CMMobDwarf` spawns with `DwarfNewMed` body.
- The body has chest, head, groin, two arms, two hands, two legs, two feet.
- Groin has kidneys.
- Head has brain and eyes.
- Hands are still added and removed correctly.
- Legs still affect movement speed.
- `CMMobHuman` and xeno mobs are unchanged.
- Xenos do not have `DwarfAnatomyComponent`.
- Dwarf critical threshold is 100 and death threshold is 200.

### Tests

- Dwarf body tree integration test.
- Non-dwarf/xeno isolation test.
- Threshold test.
- Existing `Content.IntegrationTests/Tests/Body` suite.

## 5. Phase 1a - Part damage ledger

### Deliverables

- Implement `DwarfBodyPartDamageSystem`.
- Route brute and burn damage from `DamageChangedEvent` to a part.
- Implement weighted random zone selection.
- Implement selected-zone lookup from `DamageChangedEvent.Origin`.
- Add ledger fields and reconciliation.
- Add per-part damage to the scanner state.
- Keep global wounds and bleeding working.

### Damage flow

1. `DamageableSystem` applies damage to the mob and raises
   `DamageChangedEvent`.
2. `DwarfBodyPartDamageSystem` checks `DwarfAnatomyComponent`.
3. If damage increased, select a part:
   - Use the origin's `DwarfBodyZoneTargetComponent` if present.
   - Otherwise use weighted random.
4. Apply brute/burn delta to the part's `DamageableComponent`.
5. Update the ledger.
6. If damage decreased, reduce part damage proportionally.
7. Reconciliation runs once per second.

### Acceptance criteria

- Damage lands on parts.
- Global and part damage remain consistent after damage and healing.
- Toxin and airloss damage remain systemic.
- Chest and groin take local damage but cannot be delimbed.
- Human, moth, and xeno mobs are unaffected.
- No delimbing yet.

### Tests

- Damage lands on the selected part.
- Weighted random selection covers all valid parts.
- Healing reconciles part damage.
- Ledger remains valid after many damage/heal cycles.
- Non-NewMed mobs do not get part damage.

## 6. Phase 1b - Body-zone selector and delimbing

### Deliverables

- Implement bottom-right body-zone selector UI.
- Add client-to-server selection event.
- Add server-side validation and fallback.
- Implement slash delimb chance.
- Implement `DwarfSeverSystem.TrySeverPart`.
- Add stump bleeding source.
- Add `SeveredLimbComponent`.
- Add selected-zone admin command for testing.
- Add scanner data for missing limbs and part damage.

### Delimbing behavior

- Remove child parts first through containers.
- Detach the limb subtree and drop it.
- Move local damage into `LostLimb*`.
- Add stump bleeding.
- Do not add extra damage.
- Head severing causes death and unrevivability.
- Block brain-to-mind transfer for decapitation.
- Chest and groin never sever.

### Acceptance criteria

- Player can select zones from the overlay.
- Selected zone is replicated to the server.
- Player attacks and projectiles use the selected zone.
- NPC/xeno attacks use weighted random.
- Slash below the part threshold never severs.
- Slash above the threshold rolls a scaling chance.
- Chest and groin never sever.
- Severing a hand removes the hand slot and hides the sprite.
- Severing an arm removes the hand as well.
- Severing a leg detaches the foot and slows movement.
- Severing a foot slows movement.
- Severing a head kills and makes the patient unrevivable.
- Global damage does not change from severing itself.
- Stump bleeding is present and can be treated later.

### Tests

- Delimb chance boundary tests.
- Chest/groin immunity tests.
- Hand/arm/leg/foot/head sever tests.
- No-extra-damage test.
- Hand slot removal test.
- Movement penalty test.
- Selected zone network test.
- Projectile zone test.
- Decapitation mind-transfer test.

## 7. Phase 2 - Fractures and internal bleeding

### Deliverables

- Implement fracture roll and condition.
- Implement movement and item-drop effects.
- Add splint item and splint application.
- Add splint removal verb.
- Implement internal bleeding condition.
- Implement central bleeding aggregator.
- Refactor `WoundsSystem` to write to the aggregator instead of directly to
  `BloodstreamComponent.BleedAmount`.
- Add surgeries:
  - Bone Repair.
  - Internal Bleeding.
  - Seal Stump.
- Add scanner data and health analyzer advice.
- Add body scanner record data.

### Acceptance criteria

- Fractures trigger at the configured threshold.
- Splints block fracture penalties.
- Unsplinted leg/chest/groin fractures can cause internal bleeding.
- Internal bleeding is not stopped by gauze.
- Internal bleeding surgery stops it.
- Bone repair surgery removes the fracture.
- Seal stump surgery stops stump bleeding.

### Tests

- Fracture trigger and chance test.
- Splint effect test.
- Unsplinted movement internal-bleeding test.
- Bleeding aggregator math test.
- Surgery completion tests.
- Scanner data test.

## 8. Phase 3 - Organs

### Deliverables

- Add `DwarfOrganHealthComponent` data to all NewMed organ prototypes.
- Route part damage to organs.
- Implement organ effects update loop.
- Implement organ repair surgery.
- Integrate heart and brain with defibrillator and unrevivable logic.
- Add organ damage to scanner, records, and autodoc data.
- Add heartbreak HUD support if desired.

### Acceptance criteria

- Heart damage causes asphyxiation/bloodloss.
- Heart rupture blocks defibrillation.
- Lung damage causes asphyxiation.
- Kidney/liver damage causes toxin buildup.
- Brain damage causes symptoms and eventually unrevivability.
- Eye damage causes blur/blindness.
- Organ repair surgery fixes organs.
- Defibrillator correctly refuses heartbreak and brain-dead patients.

### Tests

- Organ damage routing test.
- Heart asphyxiation test.
- Heart defib-block test.
- Lung asphyxiation test.
- Kidney/liver toxin test.
- Brain symptom test.
- Organ repair surgery test.
- Decapitation/brain-dead test.

## 9. Phase 4 - Integration and polish

### Deliverables

- Add body selector injury icons.
- Add guidebook entries and locale.
- Add autodoc procedures and research upgrades if applicable.
- Add medical records and body scanner output.
- Add admin commands and VV support.
- Handle rejuvenate by clearing all NewMed conditions.
- Add save/load coverage.
- Run performance checks.
- Complete balance pass.

### Acceptance criteria

- New conditions are visible in all medical UIs.
- Autodoc can perform supported procedures.
- Rejuvenate clears fractures, bleeding, stump, and organ damage.
- Save/load preserves part and organ state.
- No significant per-hit performance regression.

## 10. Phase 5 - Optional

- Pain system.
- Limb reattachment and prosthetics.
- Limb-specific wound migration.
- Shrapnel and foreign objects.
- Eschar and severe burns.
- Infections.
- Advanced chemistry interactions.
- Per-zone damage multipliers.

## 11. Test matrix

| Area | Test |
|---|---|
| Scope | Dwarf gets NewMed; humans and xenos do not |
| Body | Groin, hands, legs, feet exist and behave |
| Thresholds | Dwarf crits at 100, dies at 200 |
| Part damage | Damage lands on selected/random part |
| Ledger | Global/part totals stay consistent |
| Delimb | Slash below X never severs; above X scales |
| Delimb immunity | Chest and groin never sever |
| Delimb cost | No extra damage from severing |
| Hands | Hand slot removed on hand/arm sever |
| Movement | Foot/leg loss slows movement |
| Head | Decapitation kills and prevents mind transfer |
| Selector | Selection replicates and routes attacks |
| Treatment | Selected zone is used by treatment |
| Fracture | Trigger, penalties, splint, surgery |
| Internal bleeding | Cause, persistence, surgery |
| Organs | Damage, effects, repair, defib integration |
| Records | Scanner and body scanner show new data |
| Persistence | Save/load preserves NewMed state |

## 12. Verification commands

```bash
dotnet build --configuration DebugOpt --no-restore /m
dotnet test --no-build --configuration DebugOpt Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0
dotnet test --no-build --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed
```

Run the YAML linter and prototype validation after adding new prototypes.

## 13. Rollout

- Keep `dwarf.newmed.enabled` on during EvilMed development.
- Add a server-side guard in every NewMed system so the feature can be disabled
  without removing prototypes.
- Keep global wounds in place until the NewMed damage ledger and treatment flow
  are proven stable.
- Do not enable NewMed on any non-dwarf species.

## 14. Branch policy

All NewMed work should be committed to the `EvilMed` branch. Do not commit unless
explicitly requested.
