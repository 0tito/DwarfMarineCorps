# DwarfMed (NewMed) Documentation

Branch: `EvilMed`

This directory contains the design and implementation plan for DwarfMed, the
per-limb medical system for DwarfMarineCorps.

Naming note: `NewMed` is the design name. The implementation uses `DwarfMed`,
`DwarfMedBaseMobSpeciesOrganic`, and the body prototype `DMCDwarf`.

## Documents

- [Design](design.md) - anatomy, damage, delimbing, fractures, internal
  bleeding, organs, UI, and integration details.
- [Implementation Plan](implementation-plan.md) - phased work plan, file
  layout, acceptance criteria, and test matrix.
- [Phase 1 Guide](phase-1-guide.md) - completed body-zone damage, selector UI,
  event-driven delimbing, fire behavior, and test guide.
- [Phase 2 Guide](phase-2-guide.md) - fractures, splints, internal bleeding,
  and treatment implementation guide.
- [Phase 3 Guide](phase-3-guide.md) - organs, organ effects, gel inserter,
  organ repair, and defibrillator integration.
- [Current State](current-state.md) - actual implementation file map, test
  status, and starting point for a fresh session.

## Locked Decisions

| Area | Decision |
|---|---|
| Scope | Dwarf only |
| Merge strategy | New code, prototypes, assets, and locale under `_DwarfMC` |
| Xenos | Do not use DwarfMed anatomy; player-controlled xenos do get the body-zone selector for attack targeting |
| Groin | Real new `BodyPartType.Groin` |
| Damage | Body takes full damage; targeted limb receives `LimbDamageShare` as local delimb progress |
| Delimbable parts | Head, arms, hands, legs, feet |
| Non-dismemberable parts | Chest and groin |
| Delimb cost | No extra damage; stump bloodloss only |
| Global thresholds | DwarfMed dwarf: 100 critical, 200 dead |
| Hit selection | Bottom-right body overlay; base sprite plus selected-region overlay as authored |
| Hit weights | Only used for weighted random NPC attacks and fallback selection |
| Delimb events | `DwarfDelimbAttemptEvent` when a limb accumulator crosses its threshold; `DwarfSeverRequestEvent` for guaranteed severing |
| Limb accumulators | Separate Slash, Brute, and Burn counters with independent thresholds |
| Healing | Doctor-focused healing also reduces the selected limb accumulators; limb-only treatment is allowed at full body health |
| Fractures | Chance-based from slash and brute; burn never fractures; no hard threshold |
| Splints | Block fracture effects and reduce movement penalties; do not heal the fracture |
| Internal bleeding | From severe head/chest/groin brute damage and unsplinted fracture movement; surgery-only cure |
| Stump bleeding | Gauze/trauma kits slow it; Seal Stump surgery clears it |
| Fire | Fire burn damage hits every limb, and fire delimbing turns the limb into `Ash` instead of dropping it |
| CVars | `dmc.dwarfmed.enabled`, fracture/internal-bleed chance multipliers, internal-bleed rate multiplier, organ-damage chance/rate multipliers |
| Wounds | Existing global `WoundedComponent` retained for v1; migration deferred |
| Organs | Organ damage, rupture, effects, repair surgery, and per-organ gels |
| Gels | One refillable gel inserter for bone and organ repair; exact-content checks |
| Wrong gel | Organ repair: 5 organ damage + 5 Poison; bone repair consumes only |
| Defib | Defibrillation adds heart damage; heart rupture blocks defib |
| Pain | Later phase |
| Blood types | Out of scope |

## Status

Phase 0, Phase 1, Phase 2, and Phase 3 are complete. Phase 4 is next.
