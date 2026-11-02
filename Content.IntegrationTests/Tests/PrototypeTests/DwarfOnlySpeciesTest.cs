using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.PrototypeTests;

[TestFixture]
public sealed class DwarfOnlySpeciesTest
{
    [Test]
    public async Task OnlyDwarfIsRoundStart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypeManager = server.ProtoMan;

        Assert.That(SharedHumanoidAppearanceSystem.DefaultSpecies.Id, Is.EqualTo(SpeciesRestrictions.AllowedSpecies.Id),
            "The default species must be the only allowed species. An upstream merge likely reverted it to Human.");

        await server.WaitAssertion(() =>
        {
            var roundStart = prototypeManager.EnumeratePrototypes<SpeciesPrototype>()
                .Where(proto => proto.RoundStart)
                .Select(proto => proto.ID)
                .OrderBy(id => id)
                .ToArray();

            Assert.That(roundStart, Is.EqualTo(new[] { SpeciesRestrictions.AllowedSpecies.Id }),
                "Only Dwarf may be round-start. An upstream merge likely reintroduced species; re-apply the dwarf-only changes.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpeciesWeightsOnlyContainDwarf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypeManager = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            foreach (var weights in prototypeManager.EnumeratePrototypes<WeightedRandomSpeciesPrototype>())
            {
                var disallowed = weights.Weights.Keys
                    .Where(species => !SpeciesRestrictions.IsAllowed(species))
                    .OrderBy(species => species)
                    .ToArray();

                Assert.That(disallowed, Is.Empty,
                    $"WeightedRandomSpeciesPrototype '{weights.ID}' contains non-Dwarf species. An upstream merge likely reintroduced species; re-apply the dwarf-only changes.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
