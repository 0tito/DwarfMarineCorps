using System.Linq;
using Content.Server._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Stacks;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedFractureTest
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
    public async Task SlashDamageCanFracture()
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
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var part = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            part.FractureScale = 50;
            part.FractureChanceMultiplier = 1000;

            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 40);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage,
                    Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(entMan.TryGetComponent<DwarfBodyPartConditionComponent>(arm, out var condition), Is.True);
                Assert.That(condition!.Fractured, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BurnDamageDoesNotFracture()
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
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            var part = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            part.FractureScale = 50;
            part.FractureChanceMultiplier = 1000;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var heat = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Heat"), 40);
            damageable.TryChangeDamage(dwarf, heat, ignoreResistances: true, origin: attacker);

            Assert.That(
                entMan.TryGetComponent<DwarfBodyPartConditionComponent>(arm, out var condition) &&
                condition!.Fractured,
                Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FracturePersistsThroughBodyHealing()
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
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            var part = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            part.FractureScale = 50;
            part.FractureChanceMultiplier = 1000;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 40);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: attacker);

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(arm);
            Assert.That(condition.Fractured, Is.True);

            var before = entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage;
            var heal = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), -10);
            damageable.TryChangeDamage(dwarf, heal, ignoreResistances: true, origin: attacker);

            Assert.Multiple(() =>
            {
                Assert.That(condition.Fractured, Is.True);
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage,
                    Is.LessThan(before));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SplintedFractureBlocksItemDrop()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var handsSystem = entMan.System<SharedHandsSystem>();
        var fractureEffects = entMan.System<DwarfFractureEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            var item = entMan.SpawnEntity("CMGauze1", map.MapCoords);
            var hands = entMan.GetComponent<HandsComponent>(dwarf);
            Assert.That(handsSystem.TryPickup(dwarf, item, hands.ActiveHandId, false, false, false, hands), Is.True);

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            foreach (var (id, part) in bodySystem.GetBodyChildren(dwarf, body))
            {
                if (part.PartType is not (BodyPartType.Arm or BodyPartType.Hand))
                    continue;

                var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(id);
                condition.Fractured = true;
                condition.Splinted = true;
            }
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(fractureEffects.TryFractureDrop(dwarf, 1f), Is.False);

            var body = entMan.GetComponent<BodyComponent>(dwarf);
            foreach (var (id, part) in bodySystem.GetBodyChildren(dwarf, body))
            {
                if (part.PartType is not (BodyPartType.Arm or BodyPartType.Hand))
                    continue;

                entMan.GetComponent<DwarfBodyPartConditionComponent>(id).Splinted = false;
            }

            Assert.That(fractureEffects.TryFractureDrop(dwarf, 1f), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FractureChanceScalesWithTrauma()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var fractureSystem = entMan.System<DwarfFractureSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid arm = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            var part = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            part.FractureScale = 100;
            part.FractureChanceMultiplier = 1;
            part.SlashDamage = FixedPoint2.Zero;
            part.BruteDamage = FixedPoint2.Zero;

            var low = fractureSystem.ComputeFractureChance(part, FixedPoint2.New(10));

            part.BruteDamage = FixedPoint2.New(50);
            var high = fractureSystem.ComputeFractureChance(part, FixedPoint2.New(10));

            Assert.That(high, Is.GreaterThan(low));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SplintAppliesAndConsumesStack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var splintSystem = entMan.System<DwarfSplintSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid arm = default;
        EntityUid splint = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(arm);
            condition.Fractured = true;

            splint = entMan.SpawnEntity("RMCSplint10", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var stack = entMan.GetComponent<StackComponent>(splint);
            Assert.That(stack.Count, Is.EqualTo(10));

            Assert.That(splintSystem.TryApplySplint(dwarf, dwarf, splint, BodyZone.LeftArm), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DwarfBodyPartConditionComponent>(arm).Splinted, Is.True);
                Assert.That(stack.Count, Is.EqualTo(9));
            });

            Assert.That(splintSystem.TryApplySplint(dwarf, dwarf, splint, BodyZone.LeftArm), Is.False);
            Assert.That(stack.Count, Is.EqualTo(9));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectTraumaKitUseStartsLimbTreatment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid healer = default;
        EntityUid kit = default;
        EntityUid arm = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            healer = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(healer);
            target.SelectedZone = BodyZone.LeftArm;

            kit = entMan.SpawnEntity("CMTraumaKit10", map.MapCoords);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);

            var slash = new DamageSpecifier(server.ProtoMan.Index<DamageTypePrototype>("Slash"), 30);
            damageable.TryChangeDamage(dwarf, slash, ignoreResistances: true, origin: healer);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var ev = new InteractUsingEvent(healer, kit, dwarf, entMan.GetComponent<TransformComponent>(dwarf).Coordinates);
            entMan.EventBus.RaiseLocalEvent(dwarf, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(entMan.GetComponent<DwarfBodyPartComponent>(arm).SlashDamage,
                    Is.EqualTo(FixedPoint2.New(15)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegFractureMovementPenaltyIsReducedBySplint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid leg = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            leg = GetPart(entMan, bodySystem, dwarf, BodyPartType.Leg, BodyPartSymmetry.Left);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var baselineEvent = new RefreshMovementSpeedModifiersEvent();
            entMan.EventBus.RaiseLocalEvent(dwarf, baselineEvent);
            var baseline = baselineEvent.WalkSpeedModifier;

            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(leg);
            condition.Fractured = true;
            condition.Splinted = false;

            var unsplinted = new RefreshMovementSpeedModifiersEvent();
            entMan.EventBus.RaiseLocalEvent(dwarf, unsplinted);
            Assert.That(unsplinted.WalkSpeedModifier / baseline, Is.EqualTo(0.65f).Within(0.001f));

            condition.Splinted = true;

            var splinted = new RefreshMovementSpeedModifiersEvent();
            entMan.EventBus.RaiseLocalEvent(dwarf, splinted);
            Assert.That(splinted.WalkSpeedModifier / baseline, Is.EqualTo(0.9f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
