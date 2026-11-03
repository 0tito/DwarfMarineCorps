using System.Linq;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedBodyTest
{
    [Test]
    public async Task DwarfUsesDwarfMedBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        await server.WaitAssertion(() => dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<DwarfAnatomyComponent>(dwarf), Is.True);

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            Assert.That(body.Prototype?.Id, Is.EqualTo("DMCDwarf"));

            var parts = bodySystem.GetBodyChildren(dwarf, body).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(parts, Has.Length.EqualTo(11));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Head), Is.EqualTo(1));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Torso), Is.EqualTo(1));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Groin), Is.EqualTo(1));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Arm), Is.EqualTo(2));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Hand), Is.EqualTo(2));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Leg), Is.EqualTo(2));
                Assert.That(parts.Count(p => p.Component.PartType == BodyPartType.Foot), Is.EqualTo(2));
            });

            var organs = bodySystem.GetBodyOrgans(dwarf, body).ToArray();
            var organTypes = organs
                .Select(o => entMan.GetComponent<DwarfOrganHealthComponent>(o.Id).OrganType)
                .ToHashSet();

            Assert.That(organs, Has.Length.EqualTo(7));
            Assert.That(organTypes, Is.EquivalentTo(new[]
            {
                DwarfOrganType.Brain,
                DwarfOrganType.Eyes,
                DwarfOrganType.Heart,
                DwarfOrganType.Lungs,
                DwarfOrganType.Stomach,
                DwarfOrganType.Liver,
                DwarfOrganType.Kidneys,
            }));

            var thresholds = entMan.GetComponent<MobThresholdsComponent>(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(thresholds.Thresholds[FixedPoint2.New(100)], Is.EqualTo(MobState.Critical));
                Assert.That(thresholds.Thresholds[FixedPoint2.New(200)], Is.EqualTo(MobState.Dead));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonDwarfsAreUnaffected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid xeno = default;
        await server.WaitAssertion(() =>
        {
            human = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            xeno = entMan.SpawnEntity("CMXenoLarva", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfAnatomyComponent>(human), Is.False);
                Assert.That(entMan.HasComponent<DwarfAnatomyComponent>(xeno), Is.False);
                Assert.That(entMan.GetComponent<BodyComponent>(human).Prototype?.Id, Is.EqualTo("Human"));
                Assert.That(entMan.GetComponent<BodyComponent>(xeno).Prototype?.Id, Is.EqualTo("Xenonid"));
            });
        });

        await pair.CleanReturnAsync();
    }
}
