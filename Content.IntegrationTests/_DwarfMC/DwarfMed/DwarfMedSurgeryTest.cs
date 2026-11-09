using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedSurgeryTest
{
    private static EntityUid GetTorso(IEntityManager entMan, SharedBodySystem bodySystem, EntityUid dwarf)
    {
        var body = entMan.GetComponent<BodyComponent>(dwarf);
        return bodySystem.GetBodyChildrenOfType(dwarf, BodyPartType.Torso, body).First().Id;
    }

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
    public async Task BoneRepairSurgeryClearsFracture()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid boneGel = default;
        EntityUid boneSetter = default;
        EntityUid cautery = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            boneGel = entMan.SpawnEntity("CMBoneGel", map.MapCoords);
            boneSetter = entMan.SpawnEntity("CMBonesetter", map.MapCoords);
            cautery = entMan.SpawnEntity("CMCautery", map.MapCoords);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.Fractured = true;
            condition.Splinted = true;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var surgery = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryBoneRepair"));
            var gelStep = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepBoneGel"));
            var setterStep = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepBoneSetter"));
            Assert.That(surgery, Is.Not.Null);
            Assert.That(gelStep, Is.Not.Null);
            Assert.That(setterStep, Is.Not.Null);

            var valid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery!.Value, ref valid);
            Assert.That(valid.Cancelled, Is.False);

            var gelComp = entMan.GetComponent<CMSurgeryStepComponent>(gelStep!.Value);
            Assert.That(gelComp.Add, Is.Not.Null);

            var gelEvent = new CMSurgeryStepEvent(dwarf, dwarf, torso, new List<EntityUid> { boneGel });
            entMan.EventBus.RaiseLocalEvent(gelStep.Value, ref gelEvent);
            Assert.That(entMan.HasComponent<DwarfBoneGelledComponent>(torso), Is.True);

            var check = new CMSurgeryStepCompleteCheckEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(setterStep!.Value, ref check);
            Assert.That(check.Cancelled, Is.True);

            var setterEvent = new CMSurgeryStepEvent(dwarf, dwarf, torso, new List<EntityUid> { boneSetter });
            entMan.EventBus.RaiseLocalEvent(setterStep.Value, ref setterEvent);

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(condition.Fractured, Is.False);
                Assert.That(condition.Splinted, Is.False);
                Assert.That(entMan.HasComponent<DwarfBoneGelledComponent>(torso), Is.True);
            });

            var repaired = new CMSurgeryStepCompleteCheckEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(setterStep.Value, ref repaired);
            Assert.That(repaired.Cancelled, Is.False);

            Assert.That(surgerySystem.IsStepComplete(dwarf, torso, new EntProtoId("DwarfSurgeryStepBoneGel")),
                Is.True);

            entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
            entMan.EnsureComponent<CMBleedersClampedComponent>(torso);
            entMan.EnsureComponent<CMSkinRetractedComponent>(torso);

            var incised = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref incised);
            Assert.That(incised.Cancelled, Is.False);

            var closeStep = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepCloseIncision"));
            Assert.That(closeStep, Is.Not.Null);
            var closeEvent = new CMSurgeryStepEvent(dwarf, dwarf, torso, new List<EntityUid> { cautery });
            entMan.EventBus.RaiseLocalEvent(closeStep!.Value, ref closeEvent);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(torso), Is.False);
                Assert.That(entMan.HasComponent<DwarfBoneGelledComponent>(torso), Is.False);
            });

            var invalid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref invalid);
            Assert.That(invalid.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LimbReattachmentSurgeryReattachesSeveredArm()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid attacker = default;
        EntityUid weapon = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid hand = default;
        var handsBefore = 0;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            attacker = entMan.SpawnEntity("CMMobHuman", map.MapCoords);
            var target = entMan.AddComponent<BodyZoneTargetComponent>(attacker);
            target.SelectedZone = BodyZone.LeftArm;

            weapon = entMan.SpawnEntity("DwarfDebugDelimb", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            hand = GetPart(entMan, bodySystem, dwarf, BodyPartType.Hand, BodyPartSymmetry.Left);
            handsBefore = entMan.GetComponent<HandsComponent>(dwarf).Hands.Count;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var hit = new MeleeHitEvent(new List<EntityUid> { dwarf }, attacker, weapon, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(weapon, hit);
            Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(arm), Is.True);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var surgery = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryReattachLimb"));
            var step = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepReattachLimb"));
            Assert.That(surgery, Is.Not.Null);
            Assert.That(step, Is.Not.Null);

            var valid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery!.Value, ref valid);
            Assert.That(valid.Cancelled, Is.False);

            var stepEvent = new CMSurgeryStepEvent(attacker, dwarf, torso, new List<EntityUid> { arm });
            entMan.EventBus.RaiseLocalEvent(step!.Value, ref stepEvent);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(arm), Is.False);
                Assert.That(bodySystem.GetBodyChildrenOfType(dwarf, BodyPartType.Arm).Any(p => p.Id == arm), Is.True);
                Assert.That(entMan.GetComponent<BodyPartComponent>(arm).Body, Is.EqualTo(dwarf));
            });

            entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
            var incised = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref incised);
            Assert.That(incised.Cancelled, Is.False);

            entMan.RemoveComponent<CMIncisionOpenComponent>(torso);
            var invalid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref invalid);
            Assert.That(invalid.Cancelled, Is.True);

            var handValid = new CMSurgeryValidEvent(dwarf, arm);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref handValid);
            Assert.That(handValid.Cancelled, Is.False);

            var handEvent = new CMSurgeryStepEvent(attacker, dwarf, arm, new List<EntityUid> { hand });
            entMan.EventBus.RaiseLocalEvent(step.Value, ref handEvent);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(hand), Is.False);
                Assert.That(entMan.GetComponent<BodyPartComponent>(hand).Body, Is.EqualTo(dwarf));
                Assert.That(entMan.GetComponent<HandsComponent>(dwarf).Hands.Count, Is.EqualTo(handsBefore));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalBleedingSurgeryClearsInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.InternalBleeding = true;
            condition.InternalBleedRate = 0.5f;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var surgery = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryInternalBleeding"));
            var step = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepStopInternalBleeding"));
            Assert.That(surgery, Is.Not.Null);
            Assert.That(step, Is.Not.Null);

            var valid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery!.Value, ref valid);
            Assert.That(valid.Cancelled, Is.False);

            var stepEvent = new CMSurgeryStepEvent(dwarf, dwarf, torso, new List<EntityUid>());
            entMan.EventBus.RaiseLocalEvent(step!.Value, ref stepEvent);

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(condition.InternalBleeding, Is.False);
                Assert.That(condition.InternalBleedRate, Is.EqualTo(0f));
            });

            var invalid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref invalid);
            Assert.That(invalid.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SealStumpSurgeryClearsStumpBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.StumpBleeding = true;
            condition.StumpBleedRate = 0.6f;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var surgery = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgerySealStump"));
            var step = surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepSealStump"));
            Assert.That(surgery, Is.Not.Null);
            Assert.That(step, Is.Not.Null);

            var valid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery!.Value, ref valid);
            Assert.That(valid.Cancelled, Is.False);

            var stepEvent = new CMSurgeryStepEvent(dwarf, dwarf, torso, new List<EntityUid>());
            entMan.EventBus.RaiseLocalEvent(step!.Value, ref stepEvent);

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(condition.StumpBleeding, Is.False);
                Assert.That(condition.StumpBleedRate, Is.EqualTo(0f));
            });

            var invalid = new CMSurgeryValidEvent(dwarf, torso);
            entMan.EventBus.RaiseLocalEvent(surgery.Value, ref invalid);
            Assert.That(invalid.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
