using System.Collections.Generic;
using System.Linq;
using Content.Server._DwarfMC.Medical.DwarfMed;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared._DwarfMC.Medical.DwarfMed.Surgery;
using Content.Shared._RMC14.Medical.Autodoc;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfMedPhase4Test
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

    private static EntityUid GetTorso(IEntityManager entMan, SharedBodySystem bodySystem, EntityUid dwarf)
    {
        return GetPart(entMan, bodySystem, dwarf, BodyPartType.Torso);
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

    private static void RaiseStep(
        IEntityManager entMan,
        CMSurgerySystem surgerySystem,
        string stepId,
        EntityUid user,
        EntityUid body,
        EntityUid part,
        params EntityUid[] tools)
    {
        var step = surgerySystem.GetSingleton(new EntProtoId(stepId));
        Assert.That(step, Is.Not.Null);
        var ev = new CMSurgeryStepEvent(user, body, part, tools.ToList());
        entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);
    }

    private static void RaiseValid(
        IEntityManager entMan,
        CMSurgerySystem surgerySystem,
        string surgeryId,
        EntityUid body,
        EntityUid part,
        bool expected)
    {
        var surgery = surgerySystem.GetSingleton(new EntProtoId(surgeryId));
        Assert.That(surgery, Is.Not.Null);
        var ev = new CMSurgeryValidEvent(body, part);
        entMan.EventBus.RaiseLocalEvent(surgery!.Value, ref ev);
        Assert.That(ev.Cancelled, Is.EqualTo(!expected));
    }

    [Test]
    public async Task AmputationSeversArmAndHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid hand = default;
        EntityUid saw = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            hand = GetPart(entMan, bodySystem, dwarf, BodyPartType.Hand, BodyPartSymmetry.Left);
            saw = entMan.SpawnEntity("CMCircularSaw", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryAmputation", dwarf, arm, true);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryAmputation", dwarf, torso, false);

            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepAmputate", dwarf, dwarf, arm, saw);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BodyPartComponent>(arm).Body, Is.Null);
                Assert.That(entMan.GetComponent<BodyPartComponent>(hand).Body, Is.Null);
                Assert.That(entMan.HasComponent<DwarfSeveredLimbComponent>(arm), Is.True);
                Assert.That(entMan.GetComponent<DwarfBodyPartConditionComponent>(torso).StumpBleeding, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeartRemovalAndReinsertion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid hemostat = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);
            hemostat = entMan.SpawnEntity("CMHemostat", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRemoval", dwarf, torso, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRemoveHeart", dwarf, dwarf, torso, hemostat);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<OrganComponent>(heart).Body, Is.Null);
                Assert.That(entMan.HasComponent<DwarfExtractedOrganComponent>(heart), Is.True);
            });

            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartInsertion", dwarf, torso, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepInsertHeart", dwarf, dwarf, torso, heart);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<OrganComponent>(heart).Body, Is.EqualTo(dwarf));
                Assert.That(entMan.HasComponent<DwarfExtractedOrganComponent>(heart), Is.False);
                Assert.That(bodySystem.GetBodyOrgans(dwarf).Any(o => o.Id == heart), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainRemovalIsPermanent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid head = default;
        EntityUid brain = default;
        EntityUid hemostat = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryBrainInsertion")), Is.Null);
            Assert.That(surgerySystem.GetSingleton(new EntProtoId("DwarfSurgeryStepInsertBrain")), Is.Null);

            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            head = GetPart(entMan, bodySystem, dwarf, BodyPartType.Head);
            brain = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Brain);
            hemostat = entMan.SpawnEntity("CMHemostat", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryBrainRemoval", dwarf, head, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRemoveBrain", dwarf, dwarf, head, hemostat);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<OrganComponent>(brain).Body, Is.Null);
                Assert.That(entMan.HasComponent<DwarfExtractedOrganComponent>(brain), Is.True);
            });

            effects.ProcessBody(dwarf);
            Assert.That(entMan.HasComponent<DwarfOrganUnrevivableComponent>(dwarf), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovedEyesCauseBlindness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid head = default;
        EntityUid eyes = default;
        EntityUid hemostat = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            entMan.EnsureComponent<BlindableComponent>(dwarf);
            head = GetPart(entMan, bodySystem, dwarf, BodyPartType.Head);
            eyes = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Eyes);
            hemostat = entMan.SpawnEntity("CMHemostat", map.MapCoords);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryEyeRemoval", dwarf, head, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepRemoveEyes", dwarf, dwarf, head, hemostat);

            Assert.That(entMan.GetComponent<OrganComponent>(eyes).Body, Is.Null);

            effects.ProcessBody(dwarf);
            var blindable = entMan.GetComponent<BlindableComponent>(dwarf);
            Assert.That(blindable.EyeDamage, Is.EqualTo(blindable.MaxDamage));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingKidneysAndLiverCauseToxinDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var effects = entMan.System<DwarfOrganEffectSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid kidneys = default;
        EntityUid liver = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            kidneys = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Kidneys);
            liver = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Liver);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(bodySystem.RemoveOrgan(kidneys), Is.True);
            Assert.That(bodySystem.RemoveOrgan(liver), Is.True);

            effects.ProcessBody(dwarf);

            var damageable = entMan.GetComponent<DamageableComponent>(dwarf);
            Assert.That(damageable.Damage.DamageDict.GetValueOrDefault("Poison"),
                Is.GreaterThanOrEqualTo(FixedPoint2.New(2)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpenIncisionDoesNotLeakUnrelatedSurgeries()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid leg = default;
        EntityUid heart = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            leg = GetPart(entMan, bodySystem, dwarf, BodyPartType.Leg, BodyPartSymmetry.Left);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);

            entMan.EnsureComponent<CMIncisionOpenComponent>(arm);
            entMan.EnsureComponent<CMIncisionOpenComponent>(leg);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRepair", dwarf, arm, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRemoval", dwarf, arm, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartInsertion", dwarf, arm, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRepair", dwarf, leg, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRemoval", dwarf, leg, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartInsertion", dwarf, leg, false);

            RaiseValid(entMan, surgerySystem, "DwarfSurgeryBoneRepair", dwarf, leg, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryInternalBleeding", dwarf, leg, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgerySealStump", dwarf, leg, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryReattachLimb", dwarf, arm, false);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryInstallProsthetic", dwarf, arm, false);

            entMan.GetComponent<DwarfOrganHealthComponent>(heart).Damage = 10;
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRepair", dwarf, torso, true);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartRemoval", dwarf, torso, true);
            RaiseValid(entMan, surgerySystem, "DwarfSurgeryHeartInsertion", dwarf, torso, false);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProstheticInstallBehavesLikeRoboticLimb()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var fracture = entMan.System<DwarfFractureSystem>();
        var internalBleeding = entMan.System<DwarfInternalBleedingSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid prosthetic = default;
        EntityUid prostheticHand = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            prosthetic = entMan.SpawnEntity("DwarfProstheticLeftArm", map.MapCoords);
            prostheticHand = entMan.SpawnEntity("DwarfProstheticLeftHand", map.MapCoords);

            var request = new DwarfSeverRequestEvent
            {
                Body = dwarf,
                Part = arm,
                Reason = DwarfSeverReason.Surgery,
            };
            entMan.EventBus.RaiseLocalEvent(dwarf, ref request);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var torsoCondition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            torsoCondition.StumpBleeding = true;
            torsoCondition.StumpBleedRate = 0.6f;

            RaiseValid(entMan, surgerySystem, "DwarfSurgeryInstallProsthetic", dwarf, torso, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepInstallProsthetic", dwarf, dwarf, torso, prosthetic);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BodyPartComponent>(prosthetic).Body, Is.EqualTo(dwarf));
                Assert.That(entMan.HasComponent<DwarfProstheticComponent>(prosthetic), Is.True);
                Assert.That(entMan.HasComponent<DwarfProstheticInstalledComponent>(torso), Is.True);
                Assert.That(torsoCondition.StumpBleeding, Is.False);
            });

            RaiseValid(entMan, surgerySystem, "DwarfSurgeryInstallProsthetic", dwarf, prosthetic, true);
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepInstallProsthetic", dwarf, dwarf, prosthetic, prostheticHand);
            Assert.That(entMan.GetComponent<BodyPartComponent>(prostheticHand).Body, Is.EqualTo(dwarf));

            var anatomy = new Entity<DwarfAnatomyComponent>(
                dwarf,
                entMan.GetComponent<DwarfAnatomyComponent>(dwarf));
            var part = entMan.GetComponent<DwarfBodyPartComponent>(prosthetic);
            Assert.That(fracture.TryFracture(anatomy, prosthetic, part, FixedPoint2.New(1000), 1f), Is.False);
            Assert.That(internalBleeding.TryCauseInternalBleeding(anatomy, prosthetic, part, 1f), Is.False);

            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(dwarf);
            Assert.That(humanoid.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.LArm, out var layer), Is.True);
            Assert.That(layer.Color, Is.Not.Null);

            var sever = new DwarfSeverRequestEvent
            {
                Body = dwarf,
                Part = prosthetic,
                Reason = DwarfSeverReason.Surgery,
            };
            entMan.EventBus.RaiseLocalEvent(dwarf, ref sever);

            torsoCondition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BodyPartComponent>(prosthetic).Body, Is.Null);
                Assert.That(torsoCondition.StumpBleeding, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutodocClosesProstheticIncision()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var surgerySystem = entMan.System<CMSurgerySystem>();
        var autodoc = entMan.System<DwarfAutodocSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid prosthetic = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            prosthetic = entMan.SpawnEntity("DwarfProstheticLeftArm", map.MapCoords);

            var request = new DwarfSeverRequestEvent
            {
                Body = dwarf,
                Part = arm,
                Reason = DwarfSeverReason.Surgery,
            };
            entMan.EventBus.RaiseLocalEvent(dwarf, ref request);
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            RaiseStep(entMan, surgerySystem, "DwarfSurgeryStepInstallProsthetic", dwarf, dwarf, torso, prosthetic);
            entMan.EnsureComponent<CMIncisionOpenComponent>(prosthetic);
            entMan.EnsureComponent<DwarfProstheticInstalledComponent>(prosthetic);

            autodoc.CloseDwarfIncision(prosthetic);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(prosthetic), Is.False);
                Assert.That(entMan.HasComponent<DwarfProstheticInstalledComponent>(prosthetic), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutodocCompletesDwarfProceduresAndHealsLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var bodySystem = entMan.System<SharedBodySystem>();
        var autodoc = entMan.System<DwarfAutodocSystem>();
        var map = await pair.CreateTestMap();

        EntityUid dwarf = default;
        EntityUid torso = default;
        EntityUid arm = default;
        EntityUid heart = default;
        await server.WaitAssertion(() =>
        {
            dwarf = entMan.SpawnEntity("CMMobDwarf", map.MapCoords);
            torso = GetTorso(entMan, bodySystem, dwarf);
            arm = GetPart(entMan, bodySystem, dwarf, BodyPartType.Arm, BodyPartSymmetry.Left);
            heart = GetOrgan(entMan, bodySystem, dwarf, DwarfOrganType.Heart);

            var condition = entMan.EnsureComponent<DwarfBodyPartConditionComponent>(torso);
            condition.InternalBleeding = true;
            condition.InternalBleedRate = 0.5f;
            condition.Fractured = true;
            condition.Splinted = true;

            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            organ.Damage = 25;

            var limb = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            limb.BruteDamage = 20;
            limb.BurnDamage = 20;
        });
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(autodoc.HasLimbDamage(dwarf, true), Is.True);
            Assert.That(autodoc.HasLimbDamage(dwarf, false), Is.True);
            autodoc.HealLimbAccumulators(dwarf, true, 10);
            autodoc.HealLimbAccumulators(dwarf, false, 10);

            var limb = entMan.GetComponent<DwarfBodyPartComponent>(arm);
            Assert.Multiple(() =>
            {
                Assert.That(limb.BruteDamage, Is.LessThan(FixedPoint2.New(20)));
                Assert.That(limb.BurnDamage, Is.LessThan(FixedPoint2.New(20)));
            });

            Assert.That(autodoc.TryCompleteDwarfProcedure(dwarf, dwarf, AutodocSurgeryType.InternalBleeding), Is.True);
            Assert.That(autodoc.TryCompleteDwarfProcedure(dwarf, dwarf, AutodocSurgeryType.BrokenBone), Is.True);
            Assert.That(autodoc.TryCompleteDwarfProcedure(dwarf, dwarf, AutodocSurgeryType.OrganDamage), Is.True);

            var condition = entMan.GetComponent<DwarfBodyPartConditionComponent>(torso);
            var organ = entMan.GetComponent<DwarfOrganHealthComponent>(heart);
            Assert.Multiple(() =>
            {
                Assert.That(condition.InternalBleeding, Is.False);
                Assert.That(condition.Fractured, Is.False);
                Assert.That(condition.Splinted, Is.False);
                Assert.That(organ.Damage, Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }
}
