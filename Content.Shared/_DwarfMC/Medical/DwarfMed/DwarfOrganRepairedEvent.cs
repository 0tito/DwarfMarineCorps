namespace Content.Shared._DwarfMC.Medical.DwarfMed;

[ByRefEvent]
public readonly record struct DwarfOrganRepairedEvent(EntityUid Body, EntityUid Organ);
