using Content.Server.Lathe;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.Lathe;

namespace Content.Server._RMC14.Intel.Tech;

public sealed class IntelLatheRecipesSystem : EntitySystem
{
    [Dependency] private readonly IntelSystem _intel = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TechUnlockRecipeEvent>(OnTechUnlockRecipe);
        SubscribeLocalEvent<IntelLatheRecipesComponent, LatheGetRecipesEvent>(OnGetRecipes);
    }

    private void OnTechUnlockRecipe(TechUnlockRecipeEvent ev)
    {
        var tree = _intel.EnsureTechTree();
        var unlocked = EnsureComp<IntelUnlockedRecipesComponent>(tree);
        if (!unlocked.Recipes.Add(ev.Recipe))
            return;

        var query = EntityQueryEnumerator<LatheComponent>();
        while (query.MoveNext(out var uid, out var lathe))
        {
            _lathe.UpdateUserInterfaceState(uid, lathe);
        }
    }

    private void OnGetRecipes(Entity<IntelLatheRecipesComponent> ent, ref LatheGetRecipesEvent args)
    {
        if (args.Lathe != ent.Owner ||
            !_intel.TryGetTechTree(out var tree) ||
            !TryComp(tree.Value.Owner, out IntelUnlockedRecipesComponent? unlocked))
        {
            return;
        }

        foreach (var recipe in ent.Comp.Recipes)
        {
            if (unlocked.Recipes.Contains(recipe))
                args.Recipes.Add(recipe);
        }
    }
}
