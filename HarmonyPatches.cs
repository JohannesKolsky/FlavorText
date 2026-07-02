using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?
//DONE: cookID isn't being saved

//TODO: HandleIngredientsAndQualityPostfix can't use a PipeSystem subclass // why??

namespace FlavorText;

/// <summary>
/// patch when an ingredient is registered to CompIngredients
/// patch when a meal is cooked
/// patches for processor buildings from Vanilla Expanded Framework and SYR Processor Framework
/// </summary>
[StaticConstructorOnStartup]
public static class HarmonyPatches
{

    static HarmonyPatches()
    {
        var patchType = typeof(HarmonyPatches);
        Harmony harmony = new("rimworld.hekmo.FlavorText");
        //harmony.Patch(AccessTools.Method(typeof(CompIngredients), "AllowStackWith"), postfix: new HarmonyMethod(patchType, "AllowStackWithPostfix"));
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "RegisterIngredient"), postfix: new HarmonyMethod(patchType, "RegisterIngredientPostfix"));
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), postfix: new HarmonyMethod(patchType, "MakeRecipeProductsPostfix"));
    }

    // trigger TryGetFlavorText() after a set of ingredients is added
    // this isn't helpful b/c it doesn't trigger automatically after ingredients are finished being added
/*    public static void AllowStackWithPostfix(ref CompIngredients __instance)
    {
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) { compFlavor.TriedFlavorText = false; compFlavor.ingredientsCached = null;
            Log.Message("AllowStackWithPostfix");
        }
    }*/

    // dirty ingredient cache when a new ingredient is added, forcing a recheck once TryGetFlavorText is next called
    // using this since it covers spawning a meal, cooking at a station, and dispensing nutrient paste simultaneously
    //TODO: can this be converted to mergeCompatibilityTags?
    public static void RegisterIngredientPostfix(ref CompIngredients __instance)
    {
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) { compFlavor.TriedFlavorText = false; compFlavor.ingredientsCached = null; }
    }


    // after making a product with CompIngredients, add information about how it was cooked
    public static IEnumerable<Thing> MakeRecipeProductsPostfix(IEnumerable<Thing> __result, IBillGiver billGiver, Pawn worker, List<Thing> ingredients)
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
                    compFlavor.TickCreated = GenTicks.TicksAbs;
                    compFlavor.CookID = worker?.ThingID;
                    if (ModsConfig.BiotechActive && worker?.genes is not null && worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin"))) // don't ask
                    {

                        Rand.PushState(Find.World.info.Seed + GameComponentFlavorText.Iterations);
                        if (Rand.Range(0, 20) == 0)
                        {
                            compFlavor.MealTags.Add("hairy");
                        }
                        Rand.PopState();
                    }
                }

                TryGetFlavorTextWithSubMeals(ingredients, compFlavor);
            }
            yield return product;
        }
    }

    // check for CompFlavored ingredients and try to use those FlavorDefs
    //othwerwise, do TryGetFlavorText normally
    public static void TryGetFlavorTextWithSubMeals(List<Thing> ingredients, CompFlavor compFlavor)
    {
        List<FlavorDef> ingredientFlavorDefs = [];
        foreach (var ing in ingredients)
        {
            if (ing.TryGetComp(out CompFlavor ingCompFlavor))
            {
                ingredientFlavorDefs.AddRange(ingCompFlavor.FinalFlavorDefs);
            }
        }
        compFlavor.TryGetFlavorText(ingredientFlavorDefs);
    }
}
