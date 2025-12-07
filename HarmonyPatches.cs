using HarmonyLib;
using PipeSystem;
using ProcessorFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Verse;
using System.Reflection;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?

//TODO: error on quicktest or load save about the Framework that ISN'T active

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
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "RegisterIngredient"), postfix: new HarmonyMethod(patchType, "RegisterIngredientPostfix"));
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), postfix: new HarmonyMethod(patchType, "MakeRecipeProductsPostfix"));
/*        if (ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core")) harmony.PatchCategory("VEF");*/

        if (ModsConfig.IsActive("syrchalis.processor.framework")) harmony.Patch(AccessTools.Method(AccessTools.TypeByName("syrchalis.processor.framework.CompProcessor"), "TakeOutProduct"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_TakeOutProductPostFix"));

    }
    public static void HarmonyPatch_SYR_TakeOutProductPostfix(ref ThingComp __instance, ref Thing __result)
    {
        Log.Warning("TakeOutProductPostfix");
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

    /*    // VEF: cache CompFlavor when meal is added to processor
        [HarmonyPatchCategory("VEF")]
        public static class HarmonyPatch_VEF_AddIngredient
        {
            [HarmonyPrepare]
            public static bool Prepare()
            {
                return ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
            }

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(AdvancedProcessorsManager), "AddIngredient");
            }

            public static void Prefix(ref CompAdvancedResourceProcessor comp, ref Thing thing)
            {
                if (thing.TryGetComp(out CompFlavor compFlavor))
                {
                    if (VerifyCompFlavorIntegrity(compFlavor))
                    {
                        CompFlavorUtility.ActiveProcesses.Add(comp.parent.thingIDNumber, compFlavor);
                    }
                    else Log.Error($"CompFlavor for input meal into {comp.parent} had a null field, ignoring it. Output meal CompFlavor will be regenerated. Please report.");
                }

                static bool VerifyCompFlavorIntegrity(CompFlavor compFlavor)
                {
                    if (compFlavor?.TickCreated == null) return false;
                    if (compFlavor?.MealTags == null) return false;
                    if (compFlavor.IngredientsHitPointPercentage == null) return false;
                    return true;
                }
            }
        }

        [HarmonyPatchCategory("VEF")]
        public static class HarmonyPatch_VEF_HandleIngredientsAndQuality
        {
            [HarmonyPrepare]
            public static bool Prepare()
            {
                return ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
            }

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Process), "HandleIngredientsAndQuality");
            }

            public static void Postfix(ref Thing outThing, ref Process __instance)
            {
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
        }*/


    /*    // SYR: cache CompFlavor when meal is removed from processor
        [HarmonyPatchCategory("SYR")]
        public static class HarmonyPatch_SYR_TakeOutProduct
        {
            [HarmonyPrepare]
            public static bool Prepare()
            {
                return ModsConfig.IsActive("syrchalis.processor.framework");
            }

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(CompProcessor), "TakeOutProduct");
            }

            public static void Prefix(ref ActiveProcess activeProcess, ref CompProcessor __instance)
            {
                Log.Warning("TakeOutProductPrefix");
                foreach (var ingredientThing in activeProcess.ingredientThings)
                {
                    if (ingredientThing.TryGetComp(out CompFlavor compFlavor))
                    {
                        CompFlavorUtility.ActiveProcesses.Add(__instance.parent.thingIDNumber, compFlavor);
                        break;
                    }
                }
            }
            public static void Postfix(ref CompProcessor __instance, ref Thing __result)
            {
                Log.Warning("TakeOutProductPostfix");
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
        }*/
}
