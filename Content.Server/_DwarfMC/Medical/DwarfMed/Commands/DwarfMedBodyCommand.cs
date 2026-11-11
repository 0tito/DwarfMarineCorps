using System.Linq;
using System.Text;
using Content.Server.Administration;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Administration;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;

namespace Content.Server._DwarfMC.Medical.DwarfMed.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class DwarfMedBodyCommand : ToolshedCommand
{
    [CommandImplementation]
    public string DwarfMedBody([PipedArgument] EntityUid target)
    {
        var targetName = EntityManager.GetComponent<MetaDataComponent>(target).EntityName;

        if (!EntityManager.TryGetComponent<BodyComponent>(target, out var body))
            return Loc.GetString("dwarfmed-command-no-body", ("entity", targetName));

        var bodySystem = GetSys<SharedBodySystem>();
        var builder = new StringBuilder();
        builder.AppendLine(Loc.GetString("dwarfmed-command-header", ("entity", targetName)));
        builder.AppendLine(Loc.GetString("dwarfmed-command-prototype", ("prototype", body.Prototype?.Id ?? "none")));
        builder.AppendLine(Loc.GetString("dwarfmed-command-anatomy",
            ("enabled", EntityManager.HasComponent<DwarfAnatomyComponent>(target))));

        var parts = bodySystem.GetBodyChildren(target, body).ToArray();
        builder.AppendLine(Loc.GetString("dwarfmed-command-parts", ("count", parts.Length)));
        foreach (var (id, part) in parts)
        {
            var partName = EntityManager.GetComponent<MetaDataComponent>(id).EntityName;
            builder.AppendLine($"- {partName}: {part.PartType} {part.Symmetry}");

            if (EntityManager.TryGetComponent<DwarfBodyPartComponent>(id, out var bodyPart))
            {
                builder.AppendLine(
                    $"  Slash {bodyPart.SlashDamage}/{bodyPart.SlashThreshold} " +
                    $"Brute {bodyPart.BruteDamage}/{bodyPart.BruteThreshold} " +
                    $"Burn {bodyPart.BurnDamage}/{bodyPart.BurnThreshold} " +
                    $"Scale {bodyPart.FractureScale}");

                if (EntityManager.TryGetComponent<DwarfBodyPartConditionComponent>(id, out var condition))
                {
                    builder.AppendLine(
                        $"  Fractured {condition.Fractured} Splinted {condition.Splinted} " +
                        $"InternalBleeding {condition.InternalBleeding} ({condition.InternalBleedRate}) " +
                        $"StumpBleeding {condition.StumpBleeding} ({condition.StumpBleedRate})");
                }
            }
        }

        var organs = bodySystem.GetBodyOrgans(target, body).ToArray();
        builder.AppendLine(Loc.GetString("dwarfmed-command-organs", ("count", organs.Length)));
        foreach (var (id, _) in organs)
        {
            var organ = EntityManager.GetComponentOrNull<DwarfOrganHealthComponent>(id);
            var organName = organ?.OrganType.ToString() ?? Loc.GetString("dwarfmed-command-unknown-organ");
            if (organ == null)
            {
                builder.AppendLine($"- {organName}");
                continue;
            }

            builder.AppendLine($"- {organName}: {organ.Damage}/{organ.MaxDamage} " +
                               $"Ruptured {organ.Ruptured} Vital {organ.Vital}");
        }

        return builder.ToString();
    }
}
