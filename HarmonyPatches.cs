using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?

namespace FlavorText;

/// <summary>
/// patch when an ingredient is registered to CompIngredients
/// patch when a meal is cooked
/// </summary>
[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    static HarmonyPatches()
    {
        var patchType = typeof(HarmonyPatches);
        Harmony harmony = new("rimworld.hekmo.FlavorText");
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "RegisterIngredient"), null, new HarmonyMethod(patchType, "RegisterIngredientPostFix"));
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix"));
    }

    // dirty ingredient cache when a new ingredient is added, forcing a recheck once TryGetFlavorText is next called
    public static void RegisterIngredientPostFix(ref CompIngredients __instance)
    {
        if (!__instance.parent.HasComp<CompFlavor>()) return;
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) compFlavor.TriedFlavorText = false;
    }


    // after making a product with CompIngredients, add information about how it was cooked
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, IBillGiver billGiver, Pawn worker, List<Thing> ingredients)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>())
            {
                CompFlavor compFlavor = product.TryGetComp<CompFlavor>();
                if (compFlavor != null)
                {
                    compFlavor.CookingStation = ((Thing)billGiver).def;
                    compFlavor.HourOfDay = GenLocalDate.HourOfDay(billGiver.Map);
                    // average percentage of hit points of each ingredient group (ignoring quantity in group)
                    compFlavor.IngredientsHitPointPercentage = ingredients
                        .FindAll(i => i?.def != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i.def))
                        .Where(i => i.def.useHitPoints)
                        .Sum(j => (float)j.HitPoints / j.MaxHitPoints) / ingredients.Count;
                    if (ModsConfig.BiotechActive && worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin"))) // don't ask
                    {
                        Rand.PushState(product.thingIDNumber);
                        if (Rand.Range(0, 20) == 0)
                        {
                            compFlavor.MealTags.Add("hairy");
                        }
                        Rand.PopState();
                    }
                }
            }
            yield return product;
        }
    }


}
