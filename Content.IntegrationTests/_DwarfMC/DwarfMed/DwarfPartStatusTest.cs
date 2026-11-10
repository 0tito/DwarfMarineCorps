using Content.Shared._DwarfMC.Medical.DwarfMed;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Client.ResourceManagement;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._DwarfMC.DwarfMed;

[TestFixture]
public sealed class DwarfPartStatusTest
{
    [Test]
    public async Task PartStatusTexturesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var cache = IoCManager.Resolve<IResourceCache>();
            foreach (var zone in Enum.GetValues<BodyZone>())
            {
                var rsiName = DwarfPartStatus.GetRsiName(zone);
                var path = new ResPath($"/Textures/_DwarfMC/Interface/Medical/PartStatus/{rsiName}.rsi");
                Assert.That(cache.TryGetResource<RSIResource>(path, out var resource), Is.True,
                    $"Missing status RSI: {path}");

                for (var severity = 0; severity <= (int) DwarfPartSeverity.Disabled; severity++)
                {
                    var state = $"{rsiName}_{severity}";
                    Assert.That(resource!.RSI.TryGetState(state, out _), Is.True,
                        $"Missing {state} in {path}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void SeverityUsesWorstDamageRatio()
    {
        var scan = new DwarfBodyPartScan(
            "LeftArm",
            BodyPartType.Arm,
            BodyPartSymmetry.Left,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            80,
            120,
            300,
            false,
            false,
            false,
            false,
            false,
            false);

        Assert.Multiple(() =>
        {
            Assert.That(DwarfPartStatus.GetSeverity(scan), Is.EqualTo(DwarfPartSeverity.Healthy));
            Assert.That(DwarfPartStatus.GetSeverity(scan with { BruteDamage = 60 }), Is.EqualTo(DwarfPartSeverity.ModeratelyWounded));
            Assert.That(DwarfPartStatus.GetSeverity(scan with { BurnDamage = 300 }), Is.EqualTo(DwarfPartSeverity.CriticallyWounded));
            Assert.That(DwarfPartStatus.GetSeverity(scan with { Severed = true }), Is.EqualTo(DwarfPartSeverity.Severed));
            Assert.That(DwarfPartStatus.GetSeverity(scan, dead: true), Is.EqualTo(DwarfPartSeverity.Dead));
        });
    }
}
