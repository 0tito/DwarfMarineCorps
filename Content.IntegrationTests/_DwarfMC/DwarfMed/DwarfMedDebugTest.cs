using System.Collections.Generic;
using System.Linq;
using Content.Server.Storage.Components;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._RMC14.Marines.Skills;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedDebugTest
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

    private static void Hit(IEntityManager entMan, EntityUid weapon, EntityUid user, EntityUid target)
    {
        var ev = new MeleeHitEvent(new List<EntityUid> { target }, user, weapon, new DamageSpecifier(), null);
        entMan.EventBus.RaiseLocalEvent(weapon, ev);
    }

    [Test]
    public async Task DebugCrateContainsAllTestItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid crate = default;
        await server.WaitAssertion(() => crate = entMan.SpawnEntity("DwarfMedDebugCrate", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var storage = entMan.GetComponent<EntityStorageComponent>(crate);
            var contents = storage.Contents.ContainedEntities
                .Select(id => entMan.GetComponent<MetaDataComponent>(id).EntityPrototype?.ID)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(contents, Has.Count.EqualTo(39));
                Assert.That(contents, Does.Contain("DwarfDebugSlash"));
                Assert.That(contents, Does.Contain("DwarfDebugBlunt"));
                Assert.That(contents, Does.Contain("DwarfDebugPiercing"));
                Assert.That(contents, Does.Contain("DwarfDebugBurn"));
                Assert.That(contents, Does.Contain("DwarfDebugFire"));
                Assert.That(contents, Does.Contain("DwarfDebugDelimb"));
                Assert.That(contents, Does.Contain("DwarfDebugFracture"));
                Assert.That(contents, Does.Contain("DwarfDebugInternalBleeding"));
                Assert.That(contents, Does.Contain("DwarfDebugStumpBleeding"));
                Assert.That(contents, Does.Contain("DwarfDebugOrganDamage"));
                Assert.That(contents, Does.Contain("DwarfDebugOrganRupture"));
                Assert.That(contents, Does.Contain("DwarfGelInserter"));
                Assert.That(contents, Does.Contain("DwarfBoneGelVial"));
                Assert.That(contents, Does.Contain("DwarfHeartGelVial"));
                Assert.That(contents, Does.Contain("DwarfLungGelVial"));
                Assert.That(contents, Does.Contain("DwarfBrainGelVial"));
                Assert.That(contents, Does.Contain("DwarfEyeGelVial"));
                Assert.That(contents, Does.Contain("DwarfKidneyGelVial"));
                Assert.That(contents, Does.Contain("DwarfLiverGelVial"));
                Assert.That(contents, Does.Contain("DwarfStomachGelVial"));
                Assert.That(contents, Does.Contain("RMCSplint10"));
                Assert.That(contents, Does.Contain("RMCFixOVein"));
                Assert.That(contents, Does.Contain("CMBoneGel"));
                Assert.That(contents, Does.Contain("CMBonesetter"));
                Assert.That(contents, Does.Contain("CMHealthAnalyzer"));
                Assert.That(contents, Does.Contain("DwarfDebugHealthAnalyzer"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugHealthAnalyzerHasNoDoAfterOrUseDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid analyzer = default;
        await server.WaitAssertion(() => analyzer = entMan.SpawnEntity("DwarfDebugHealthAnalyzer", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var unskilled = entMan.GetComponent<MedicallyUnskilledDoAfterComponent>(analyzer);
            var useDelay = entMan.GetComponent<UseDelayComponent>(analyzer);

            Assert.Multiple(() =>
            {
                Assert.That(unskilled.Min, Is.EqualTo(0));
                Assert.That(useDelay.Delay, Is.EqualTo(TimeSpan.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugDelimbStickSeversWithoutDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid weapon = default;
        EntityUid arm = default;
        var handsBefore = 0;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            weapon = entMan.SpawnEntity("DwarfDebugDelimb", map.MapCoords);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            handsBefore = entMan.GetComponent<HandsComponent>(dwarf).Hands.Count;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Hit(entMan, weapon, attacker, dwarf);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(arm), Is.True);
                Assert.That(entMan.GetComponent<HandsComponent>(dwarf).Hands.Count, Is.EqualTo(handsBefore - 1));
                Assert.That(entMan.GetComponent<DamageableComponent>(dwarf).Damage.GetTotal(),
                    Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugFractureStickFracturesWithoutDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid weapon = default;
        EntityUid arm = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            weapon = entMan.SpawnEntity("DwarfDebugFracture", map.MapCoords);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Hit(entMan, weapon, attacker, dwarf);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfBodyPartConditionComponent>(arm).Fractured, Is.True);
                Assert.That(entMan.GetComponent<DamageableComponent>(dwarf).Damage.GetTotal(),
                    Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugFireStickIgnitesTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid weapon = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            entMan.AddComponent<BodyZoneTargetComponent>(attacker).SelectedZone = BodyZone.LeftArm;
            weapon = entMan.SpawnEntity("DwarfDebugFire", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Hit(entMan, weapon, attacker, dwarf);
            Assert.That(entMan.HasComponent<OnFireComponent>(dwarf), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DebugBleedingSticksApplyConditions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid bleedingDwarf = default;
        EntityUid stumpDwarf = default;
        EntityUid attacker = default;
        EntityUid bleedingWeapon = default;
        EntityUid stumpWeapon = default;
        EntityUid bleedingTorso = default;
        EntityUid stumpTorso = default;
        await server.WaitAssertion(() =>
        {
            bleedingDwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            stumpDwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.Chest;

            bleedingWeapon = entMan.SpawnEntity("DwarfDebugInternalBleeding", map.MapCoords);
            stumpWeapon = entMan.SpawnEntity("DwarfDebugStumpBleeding", map.MapCoords);

            bleedingTorso = GetPart(entMan, bodySystem, bleedingDwarf, BodyPartType.Torso, BodyPartSymmetry.None);
            stumpTorso = GetPart(entMan, bodySystem, stumpDwarf, BodyPartType.Torso, BodyPartSymmetry.None);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Hit(entMan, bleedingWeapon, attacker, bleedingDwarf);
            Hit(entMan, stumpWeapon, attacker, stumpDwarf);

            var bleeding = entMan.GetComponent<DwarfBodyPartConditionComponent>(bleedingTorso);
            var stump = entMan.GetComponent<DwarfBodyPartConditionComponent>(stumpTorso);

            Assert.Multiple(() =>
            {
                Assert.That(bleeding.InternalBleeding, Is.True);
                Assert.That(bleeding.InternalBleedRate, Is.GreaterThan(0f));
                Assert.That(entMan.HasComponent<WoundedComponent>(bleedingDwarf), Is.True);

                Assert.That(stump.StumpBleeding, Is.True);
                Assert.That(stump.StumpBleedRate, Is.GreaterThan(0f));
                Assert.That(entMan.HasComponent<WoundedComponent>(stumpDwarf), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
