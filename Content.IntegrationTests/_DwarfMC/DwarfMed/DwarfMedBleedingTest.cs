using System.Linq;
using Content.Server._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedBleedingTest
{
    private static EntityUid GetPart(
        IEntityManager entMan,
        SharedBodySystem bodySystem,
        EntityUid dwarf,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var body = entMan.GetComponent<BodyComponent>(dwarf);
        return bodySystem
            .GetBodyChildrenOfType(dwarf, type, body)
            .First(p => p.Component.Symmetry == symmetry)
            .Id;
    }

    [Test]
    public async Task UnsplintedFractureMovementCausesInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var internalBleeding = entMan.System<DwarfInternalBleedingSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);

            var part = entMan.GetComponent<DwarfBodyPartComponent>(torso);
            part.InternalBleedChanceMultiplier = 1000;

            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.Fractured = true;
            condition.Splinted = false;
            condition.InternalBleeding = false;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var anatomy = entMan.GetComponent<DwarfAnatomyComponent>(dwarf);
            internalBleeding.CheckMovement((dwarf, anatomy));

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(condition.InternalBleeding, Is.True);
                Assert.That(condition.InternalBleedRate, Is.GreaterThan(0f));
                Assert.That(entMan.HasComponent<WoundedComponent>(dwarf), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SplintedFractureMovementDoesNotCauseInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var internalBleeding = entMan.System<DwarfInternalBleedingSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);

            var part = entMan.GetComponent<DwarfBodyPartComponent>(torso);
            part.InternalBleedChanceMultiplier = 1000;

            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.Fractured = true;
            condition.Splinted = true;
            condition.InternalBleeding = false;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var anatomy = entMan.GetComponent<DwarfAnatomyComponent>(dwarf);
            internalBleeding.CheckMovement((dwarf, anatomy));

            Assert.That(entMan.GetComponent<DwarfBodyPartConditionComponent>(torso).InternalBleeding,
                Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalBleedingContributesToBleedingEvent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.InternalBleeding = true;
            condition.InternalBleedRate = 0.5f;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var ev = new DwarfBleedingEvent(0);
            entMan.EventBus.RaiseLocalEvent(dwarf, ref ev);
            Assert.That(ev.Amount, Is.EqualTo(0.5f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StumpPackingReducesRateButDoesNotClearBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var treatment = entMan.System<DwarfLimbTreatmentSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.StumpBleeding = true;
            condition.StumpBleedRate = 0.6f;

            Assert.That(treatment.TryPackStump(torso, condition), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(condition.StumpBleeding, Is.True);
                Assert.That(condition.StumpBleedRate, Is.EqualTo(0.3f).Within(0.001f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StumpBleedingAppliesBloodlossToLivingDwarf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.StumpBleeding = true;
            condition.StumpBleedRate = 0.6f;
            entMan.EnsureComponent<WoundedComponent>(dwarf);
        });
        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(dwarf);
            Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IncisionBleedingAddsToBleedingEvent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var ev = new DwarfBleedingEvent(0);
            entMan.EventBus.RaiseLocalEvent(dwarf, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Amount, Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(entMan.HasComponent<WoundedComponent>(dwarf), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadDwarfStumpBleedingContinuesAtHalfRate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            var asphyxiation = server.ProtoMan.Index<DamageTypePrototype>("Asphyxiation");
            damageable.TryChangeDamage(dwarf, new DamageSpecifier(asphyxiation, 300), ignoreResistances: true);

            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.StumpBleeding = true;
            condition.StumpBleedRate = 0.6f;
            entMan.EnsureComponent<WoundedComponent>(dwarf);
        });
        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0f));
                Assert.That(bloodstream.BleedAmount, Is.LessThan(0.6f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScannerReportsFractureSplintAndInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.Fractured = true;
            condition.Splinted = true;
            condition.InternalBleeding = true;
            condition.StumpBleeding = true;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var scans = entMan.System<DwarfBodyPartScanSystem>().Build(dwarf);
            var chest = scans.Single(s => s.SlotId == BodyZone.Chest.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(chest.Fractured, Is.True);
                Assert.That(chest.Splinted, Is.True);
                Assert.That(chest.InternalBleeding, Is.True);
                Assert.That(chest.StumpBleeding, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
