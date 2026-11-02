# NewMed ("EvilMed") Documentation

Branch: `EvilMed`

This directory contains the design and implementation plan for NewMed, a
per-limb medical system for DwarfMarineCorps.

## Documents

- [Design](design.md) - anatomy, damage, delimbing, fractures, internal
  bleeding, organs, UI, and integration details.
- [Implementation Plan](implementation-plan.md) - phased work plan, file
  layout, acceptance criteria, and test matrix.

## Locked Decisions

| Area | Decision |
|---|---|
| Scope | Dwarf only |
| Merge strategy | New code, prototypes, assets, and locale under `_DwarfMC` |
| Xenos | Unaffected; xenos use the separate `CMXenoBase` tree |
| Groin | Real new `BodyPartType.Groin` |
| Damage | Per-part damage with a slash-only delimb chance above a part threshold |
| Delimbable parts | Head, arms, hands, legs, feet |
| Non-dismemberable parts | Chest and groin |
| Delimb cost | No extra damage; stump bloodloss only |
| Global thresholds | NewMed dwarf: 100 critical, 200 dead |
| Hit selection | Bottom-right body overlay; used for attacks and treatment |
| Wounds | Existing global `WoundedComponent` retained for v1; migration deferred |
| Pain | Later phase |
| Blood types | Out of scope |

## Status

Design phase only. No gameplay code has been added yet.
