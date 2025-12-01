using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Verse;
using PipeSystem;
using ProcessorFramework;

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
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "RegisterIngredient"), null, new HarmonyMethod(patchType, "RegisterIngredientPostfix"));
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostfix"));

        //TODO: ProcessorFramework uses .NET 4.8
        if (ModLister.GetActiveModWithIdentifier("syrchalis.processor.framework") != null)
        {
            harmony.Patch(AccessTools.Method(typeof(CompProcessor), "TakeOutProduct"), new HarmonyMethod(patchType, "TakeOutProductPrefix"), new HarmonyMethod(patchType, "TakeOutProductPostfix"));
        }
    }

    // dirty ingredient cache when a new ingredient is added, forcing a recheck once TryGetFlavorText is next called
    public static void RegisterIngredientPostfix(ref CompIngredients __instance)
    {
        if (!__instance.parent.HasComp<CompFlavor>()) return;
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) compFlavor.TriedFlavorText = false;
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
                    compFlavor.CookID = worker?.thingIDNumber;
                    // average percentage of hit points of each ingredient group (ignoring quantity in group)
                    compFlavor.IngredientsHitPointPercentage = ingredients
                        .FindAll(i => i?.def != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i.def))
                        .Where(i => i.def.useHitPoints)
                        .Sum(j => (float)j.HitPoints / j.MaxHitPoints) / ingredients.Count;
                    if (ModsConfig.BiotechActive && worker?.genes is not null && worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin"))) // don't ask
                    {
                        Rand.PushState((int)compFlavor.TickCreated);
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

    // VEF: cache CompFlavor when meal is added to processor
    [HarmonyPatch(typeof(CompAdvancedResourceProcessor), "AddIngredient")]
    public static class Harmony_AddIngredient
    {
        public static void Prefix(ref CompAdvancedResourceProcessor comp, ref Thing thing)
        {
            if (ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaFactionsExpanded.Core") != null)
            {
                if (thing.TryGetComp(out CompFlavor compFlavor))
                {
                    CompFlavorUtility.ActiveProcesses.Add(comp.parent.thingIDNumber, compFlavor);
                }
            }
        }

    }

    // VEF: retrieve CompFlavor from cache when meal is removed from processor
    [HarmonyPatch(typeof(CompAdvancedResourceProcessor), "HandleIngredientsAndQuality")]
    public static class Harmony_HandleIngredientsAndQuality
    {
        public static void Postfix(ref Thing outThing, ref Process __instance)
        {
            if (ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaFactionsExpanded.Core") != null)
            {
                if (outThing.TryGetComp(out CompFlavor outCompFlavor))
                {
                    int key = __instance.advancedProcessor.parent.thingIDNumber;
                    if (CompFlavorUtility.ActiveProcesses.TryGetValue(key, out CompFlavor cachedCompFlavor))
                    {
                        outCompFlavor.TickCreated = cachedCompFlavor.TickCreated;
                        outCompFlavor.MealTags = cachedCompFlavor.MealTags;
                        outCompFlavor.IngredientsHitPointPercentage = cachedCompFlavor.IngredientsHitPointPercentage;
                        CompFlavorUtility.ActiveProcesses.Remove(key);
                    }
                }
            }
        }
    }

    // SYR: cache CompFlavor when meal is removed from processor
    public static void TakeOutProductPrefix(ref ActiveProcess activeProcess, ref CompProcessor __instance)
    {
        if (ModLister.GetActiveModWithIdentifier("syrchalis.processor.framework") != null)
        {
            foreach (var ingredientThing in activeProcess.ingredientThings)
            {
                if (ingredientThing.TryGetComp(out CompFlavor compFlavor))
                {
                    CompFlavorUtility.ActiveProcesses.Add(__instance.parent.thingIDNumber, compFlavor);
                    break;
                }
            }
        }
    }

    //SYR: retrieve CompFlavor from cache when meal is removed from processor
    public static void TakeOutProductPostfix(ref CompProcessor __instance, ref Thing __result)
    {
        if (ModLister.GetActiveModWithIdentifier("syrchalis.processor.framework") != null)
        {
            if (__result.TryGetComp(out CompFlavor outCompFlavor))
            {
                int key = __instance.parent.thingIDNumber;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(key, out CompFlavor cachedCompFlavor))
                {
                    outCompFlavor.TickCreated = cachedCompFlavor.TickCreated;
                    outCompFlavor.MealTags = cachedCompFlavor.MealTags;
                    outCompFlavor.IngredientsHitPointPercentage = cachedCompFlavor.IngredientsHitPointPercentage;
                    CompFlavorUtility.ActiveProcesses.Remove(key);
                }
            } 
        }
    }

}
