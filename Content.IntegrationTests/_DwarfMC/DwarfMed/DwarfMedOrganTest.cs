using System.Collections.Generic;
using System.Linq;
using Content.Server._DwarfMC.Medical.DwarfMed;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Traits.Assorted;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedOrganTest
{
    private static EntityUid GetPart(
        IEntityManager entMan,
        SharedBodySystem bodySystem,
        EntityUid dwarf,
        BodyPartType type,
        BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        var body = entMan.GetComponent<BodyComponent>(dwarf);
        return bodySystem
            .GetBodyChildrenOfType(dwarf, type, body)
            .First(p => symmetry == BodyPartSymmetry.None || p.Component.Symmetry == symmetry)
            .Id;
    }

    private static EntityUid GetOrgan(
        IEntityManager entMan,
        SharedBodySystem bodySystem,
        EntityUid dwarf,
        DwarfOrganType type)
    {
        return bodySystem
            .GetBodyOrgans(dwarf)
            .First(o => entMan.GetComponent<DwarfOrganHealthComponent>(o.Id).OrganType == type)
            .Id;
    }

    private static EntityUid SpawnInserter(
        IEntityManager entMan,
        SharedSolutionContainerSystem solutionSystem,
        MapCoordinates coords,
        string reagent,
        FixedPoint2 amount)
    {
        var inserter = entMan.SpawnEntity("DwarfGelInserter", coords);
        Assert.That(solutionSystem.TryGetSolution(inserter, "gel", out var solutionEnt, out _), Is.True);
        Assert.That(solutionSystem.TryAddReagent(solutionEnt!.Value, reagent, amount), Is.True);
        return inserter;
    }

    private static void RaiseStep(
        IEntityManager entMan,
        CMSurgerySystem surgerySystem,
        string stepId,
        EntityUid user,
        EntityUid body,
        EntityUid part,
        EntityUid tool)
    {
        var step = surgerySystem.GetSingleton(new EntProtoId(stepId));
        Assert.That(step, Is.Not.Null);
        var ev = new CMSurgeryStepEvent(user, body, part, new List<EntityUid> { tool });
        entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);
    }

    [Test]
    public async Task OrganDamageRoutesFromSelectedRegion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.Chest;

            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            var part = entMan.GetComponent<DwarfBodyPartComponent>(torso);
            part.OrganDamageThreshold = 10;
            part.OrganDamageChanceMultiplier = 1000;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var blunt = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Blunt"), 80);
            damageable.TryChangeDamage(dwarf, blunt, ignoreResistances: true, origin: attacker);

            var damaged = bodySystem
                .GetBodyOrgans(dwarf)
                .Any(o => entMan.GetComponent<DwarfOrganHealthComponent>(o.Id).Damage > FixedPoint2.Zero);

            Assert.That(damaged, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefibrillationDamagesHeart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid defib = default;
        EntityUid heart = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            defib = entMan.SpawnEntity("CMDefibrillatorInfinite", map.MapCoords);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var defibComp = entMan.GetComponent<DefibrillatorComponent>(defib);
            var ev = new TargetDefibrillatedEvent(dwarf, new Entity<DefibrillatorComponent>(defib, defibComp));
            entMan.EventBus.RaiseLocalEvent(dwarf, ref ev);

            Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage,
                Is.EqualTo(FixedPoint2.New(10)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeartRuptureBlocksDefibAndRepairClears()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            organ.Damage = organ.MaxDamage;
            organ.Ruptured = true;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfHeartGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            effects.ProcessBody(dwarf);
            Assert.That(entMan.HasComponent<RMCDefibrillatorBlockedComponent>(dwarf), Is.True);

            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairHeart", dwarf, dwarf, torso, inserter);

            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            Assert.Multiple(() =>
            {
                Assert.That(organ.Damage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(organ.Ruptured, Is.False);
            });

            effects.ProcessBody(dwarf);
            Assert.That(entMan.HasComponent<RMCDefibrillatorBlockedComponent>(dwarf), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainRuptureMakesUnrevivableAndRepairClears()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid head = default;
        EntityUid brain = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            head = GetPart(entMan, bodySystem, dwarf, BodyPartType.Head);
            brain = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Brain);
            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(brain);
            organ.Damage = organ.MaxDamage;
            organ.Ruptured = true;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfBrainGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            effects.ProcessBody(dwarf);
            Assert.That(entMan.HasComponent<UnrevivableComponent>(dwarf), Is.True);

            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairBrain", dwarf, dwarf, head, inserter);

            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(brain);
            Assert.Multiple(() =>
            {
                Assert.That(organ.Damage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(organ.Ruptured, Is.False);
                Assert.That(entMan.HasComponent<UnrevivableComponent>(dwarf), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EyeDamageCausesBlurAndRepairClears()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid head = default;
        EntityUid eyes = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            entMan.EnsureComponent<BlindableComponent>(dwarf);
            head = GetPart(entMan, bodySystem, dwarf, BodyPartType.Head);
            eyes = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Eyes);
            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(eyes);
            organ.Damage = organ.MaxDamage * 0.8f;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfEyeGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            effects.ProcessBody(dwarf);
            Assert.That(entMan.GetComponent<BlindableComponent>(dwarf).EyeDamage, Is.GreaterThan(0));

            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairEyes", dwarf, dwarf, head, inserter);
            effects.ProcessBody(dwarf);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(eyes).Damage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.GetComponent<BlindableComponent>(dwarf).EyeDamage, Is.EqualTo(0));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganEffectsProduceAirlossAndToxin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid lungs = default;
        EntityUid kidneys = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            lungs = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Lungs);
            kidneys = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Kidneys);
            entMan.GetComponent<DwarfOrganHealthComponent>(lungs).Damage = 50;
            entMan.GetComponent<DwarfOrganHealthComponent>(kidneys).Damage = 50;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            effects.ProcessBody(dwarf);
            var damageable = entMan.GetComponent<DamageableComponent>(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Asphyxiation"), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Poison"), Is.GreaterThan(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleOrgansRequireSeparateGels()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid lungs = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            lungs = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Lungs);
            entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage = 20;
            entMan.GetComponent<DwarfOrganHealthComponent>(lungs).Damage = 20;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfLungGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairLungs", dwarf, dwarf, torso, inserter);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(lungs).Damage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage, Is.EqualTo(FixedPoint2.New(20)));
            });

            Assert.That(solutionSystem.TryGetSolution(inserter, "gel", out var solutionEnt, out _), Is.True);
            solutionSystem.RemoveAllSolution(solutionEnt!.Value);
            Assert.That(solutionSystem.TryAddReagent(solutionEnt.Value, "DwarfHeartGel", 5), Is.True);

            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairHeart", dwarf, dwarf, torso, inserter);
            Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage, Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WrongGelDamagesOrganAndPoisons()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage = 20;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfLungGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairHeart", dwarf, dwarf, torso, inserter);

            var damageable = entMan.GetComponent<DamageableComponent>(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage,
                    Is.GreaterThanOrEqualTo(FixedPoint2.New(25)));
                Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Poison"), Is.EqualTo(FixedPoint2.New(5)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoneRepairWrongGelConsumesButDoesNotSetBone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso).Fractured = true;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfHeartGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepBoneGel", dwarf, dwarf, torso, inserter);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfBoneGelledComponent>(torso), Is.False);
                Assert.That(entMan.GetComponent<DwarfBodyPartConditionComponent>(torso).Fractured, Is.True);
                Assert.That(solutionSystem.TryGetSolution(inserter, "gel", out _, out var solution), Is.True);
                Assert.That(solution!.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }


    [Test]
    public async Task MixedGelContentsCountAsWrong()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage = 20;
            inserter = entMan.SpawnEntity("DwarfGelInserter", map.MapCoords);
            Assert.That(solutionSystem.TryGetSolution(inserter, "gel", out var solutionEnt, out _), Is.True);
            Assert.That(solutionSystem.TryAddReagent(solutionEnt!.Value, "DwarfHeartGel", FixedPoint2.New(2.5f)), Is.True);
            Assert.That(solutionSystem.TryAddReagent(solutionEnt.Value, "DwarfLungGel", FixedPoint2.New(2.5f)), Is.True);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairHeart", dwarf, dwarf, torso, inserter);

            var damageable = entMan.GetComponent<DamageableComponent>(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage,
                    Is.GreaterThanOrEqualTo(FixedPoint2.New(25)));
                Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Poison"), Is.EqualTo(FixedPoint2.New(5)));
            });
        });

        await pair.CleanReturnAsync();
    }


    [Test]
    public async Task HeartRepairDoesNotDamageOtherOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var solutionSystem = entMan.System<SharedSolutionContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid lungs = default;
        EntityUid inserter = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            lungs = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Lungs);
            entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage = 20;
            entMan.GetComponent<DwarfOrganHealthComponent>(lungs).Damage = 20;
            inserter = SpawnInserter(entMan, solutionSystem, map.MapCoords, "DwarfHeartGel", 5);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRepairHeart", dwarf, dwarf, torso, inserter);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entMan.GetComponent<DwarfOrganHealthComponent>(lungs).Damage, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(entMan.GetComponent<DamageableComponent>(dwarf).Damage.DamageDict.GetValueOrDefault("Poison"),
                    Is.EqualTo(FixedPoint2.Zero));
            });

            var step = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepRepairHeart"));
            Assert.That(step, Is.Not.Null);
            var check = new CMSurgeryStepCompleteCheckEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(step!.Value, ref check);
            Assert.That(check.Cancelled, Is.False);
        });

        await pair.CleanReturnAsync();
    }


    [Test]
    public async Task HeartBleedStartsAfterThirtyPercent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid heart = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            organ.Damage = organ.MaxDamage * 0.2f;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var below = new DwarfBleedingEvent(0);
            entMan.EventBus.RaiseLocalEvent(dwarf, ref below);
            Assert.That(below.Amount, Is.EqualTo(0f));

            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            organ.Damage = organ.MaxDamage * 0.4f;

            var above = new DwarfBleedingEvent(0);
            entMan.EventBus.RaiseLocalEvent(dwarf, ref above);
            Assert.That(above.Amount, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganScanIncludesAllOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var scans = entMan.System<DwarfBodyPartScanSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            human = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(scans.BuildOrgans(dwarf), Has.Count.EqualTo(7));
            Assert.That(scans.BuildOrgans(human), Is.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
