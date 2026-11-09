using System.Linq;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedDamageTest
{
    [Test]
    public async Task SelectedZoneReceivesLimbShareWithoutReducingBodyDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var body = entMan.GetComponent<BodyComponent>(dwarf);
            var leftArm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left);

            var dwarfDamageable = entMan.GetComponent<DamageableComponent>(dwarf);
            var before = dwarfDamageable.Damage.GetTotal();
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 50);

            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(leftArm.Id).SlashDamage,
                    Is.EqualTo(FixedPoint2.New(25)));
                Assert.That(dwarfDamageable.Damage.GetTotal() - before, Is.EqualTo(FixedPoint2.New(50)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeveringHandRemovesHandSlotAndDoesNotAddDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid hand = default;
        var handsBefore = 0;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftHand;

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            hand = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Hand, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;

            var partDamage = entMan.GetComponent<DwarfBodyPartComponent>(hand);
            partDamage.SlashThreshold = 10;
            partDamage.Dismemberable = true;

            var handPart = entMan.GetComponent<BodyPartComponent>(hand);
            Assert.Multiple(() =>
            {
                Assert.That(handPart.PartType, Is.EqualTo(BodyPartType.Hand));
                Assert.That(handPart.Symmetry, Is.EqualTo(BodyPartSymmetry.Left));
                Assert.That(handPart.ToHumanoidLayers(), Is.EqualTo(HumanoidVisualLayers.LHand));
            });

            handsBefore = entMan.GetComponent<HandsComponent>(dwarf).Hands.Count;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var dwarfDamageable = entMan.GetComponent<DamageableComponent>(dwarf);
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 20);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(hand), Is.True);
                Assert.That(entMan.GetComponent<HandsComponent>(dwarf).Hands.Count, Is.EqualTo(handsBefore - 1));
                Assert.That(dwarfDamageable.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(20)));

                var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(dwarf);
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LHand));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelectedLimbHealingReducesAccumulator()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid healer = default;
        EntityUid arm = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            healer = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(healer);
            target.SelectedZone = BodyZone.LeftArm;

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            arm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 40);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: healer);
            Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage,
                Is.EqualTo(FixedPoint2.New(20)));

            var heal = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), -10);
            damageable.TryChangeDamage(dwarf, heal, ignoreResistances: true, origin: healer);

            Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage,
                Is.EqualTo(FixedPoint2.New(10)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScannerBuildsAllLimbZones()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        await server.WaitAssertion(() => dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var scans = entMan.System<DwarfBodyPartScanSystem>().Build(dwarf);
            Assert.Multiple(() =>
            {
                Assert.That(scans, Has.Count.EqualTo(11));
                Assert.That(scans.Any(s => s.SlotId == BodyZone.LeftArm.ToString()), Is.True);
                Assert.That(scans.All(s => !s.Severed), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadDwarfKeepsTakingDamagePastOldCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        await server.WaitAssertion(() => dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var dwarfDamageable = entMan.GetComponent<DamageableComponent>(dwarf);
            var asphyxiation = server.ProtoMan.Index<DamageTypePrototype>("Asphyxiation");
            damageable.TryChangeDamage(dwarf,
                new DamageSpecifier(asphyxiation, 1000),
                ignoreResistances: true);

            var before = dwarfDamageable.Damage.GetTotal();
            damageable.TryChangeDamage(dwarf,
                new DamageSpecifier(asphyxiation, 100),
                ignoreResistances: true);

            Assert.Multiple(() =>
            {
                Assert.That(before, Is.GreaterThan(FixedPoint2.New(719)));
                Assert.That(dwarfDamageable.Damage.GetTotal(), Is.EqualTo(before + FixedPoint2.New(100)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeveringArmDropsHandAndRemovesHandSlot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid arm = default;
        EntityUid hand = default;
        var handsBefore = 0;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            arm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;
            hand = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Hand, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;

            var partDamage = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            partDamage.SlashThreshold = 10;

            handsBefore = entMan.GetComponent<HandsComponent>(dwarf).Hands.Count;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 20);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(arm), Is.True);
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(hand), Is.True);
                Assert.That(entMan.GetComponent<HandsComponent>(dwarf).Hands.Count, Is.EqualTo(handsBefore - 1));

                var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(dwarf);
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LArm));
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LHand));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeveringLegDropsFootAndLeg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid leg = default;
        EntityUid foot = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.RightLeg;

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            leg = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Leg, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Right)
                .Id;
            foot = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Foot, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Right)
                .Id;

            entMan.GetComponent<DwarfBodyPartComponent>(leg).SlashThreshold = 10;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 20);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(leg), Is.True);
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(foot), Is.True);

                var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(dwarf);
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.RLeg));
                Assert.That(humanoid.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.RFoot));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireDamagesAllLimbZones()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid leftArm = default;
        EntityUid rightArm = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            entMan.AddComponent<OnFireComponent>(dwarf);

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            leftArm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;
            rightArm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Right)
                .Id;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            damageable.TryChangeDamage(dwarf, new DamageSpecifier(heat, 20), ignoreResistances: true);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(leftArm).BurnDamage,
                    Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(rightArm).BurnDamage,
                    Is.EqualTo(FixedPoint2.New(10)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BurnThresholdDelimbTurnsLimbIntoAshWithoutFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid arm = default;
        EntityUid hand = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            arm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;
            hand = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Hand, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Left)
                .Id;

            var part = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            part.BurnThreshold = 10;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            damageable.TryChangeDamage(dwarf, new DamageSpecifier(heat, 40), ignoreResistances: true, origin: attacker);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(arm), Is.False);
                Assert.That(entMan.EntityExists(hand), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireSeverTurnsLimbIntoAsh()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid arm = default;
        EntityUid hand = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            entMan.AddComponent<OnFireComponent>(dwarf);

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            arm = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Arm, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Right)
                .Id;
            hand = bodySystem
                .GetBodyChildrenOfType(dwarf, BodyPartType.Hand, body)
                .First(p => p.Component.Symmetry == BodyPartSymmetry.Right)
                .Id;

            entMan.GetComponent<DwarfBodyPartComponent>(arm).BurnThreshold = 10;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var heat = server.ProtoMan.Index<DamageTypePrototype>("Heat");
            damageable.TryChangeDamage(dwarf, new DamageSpecifier(heat, 20), ignoreResistances: true);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(arm), Is.False);
                Assert.That(entMan.EntityExists(hand), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
