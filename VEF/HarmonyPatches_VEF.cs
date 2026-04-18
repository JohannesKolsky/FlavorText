using FlavorText;
using HarmonyLib;
using PipeSystem;
using System.Collections.Generic;
using System.Linq;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?

namespace VEF
{
    /// <summary>
    /// patches for processor buildings from VEF
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_VEF
    {
        static HarmonyPatches_VEF()
        {
            Log.Warning("VEF Patches Loading...");
            var patchType = typeof(HarmonyPatches_VEF);
            Harmony harmony = new("rimworld.hekmo.VEF");
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.AdvancedProcessorsManager"), "AddIngredient"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_AddIngredientPrefix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.Process"), "HandleIngredientsAndQuality"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix"));
            }
        }

        // VEF: cache CompFlavor when meal is added to processor
        public static void HarmonyPatch_VEF_AddIngredientPrefix(ref ThingComp comp, ref Thing thing)
        {
            Log.Warning("AddIngredientsPrefix");
            if (thing.TryGetComp(out CompFlavor compFlavor))
            {
                if (VerifyCompFlavorIntegrity(compFlavor))
                {
                    CompFlavorUtility.ActiveProcesses.Add(comp.parent.thingIDNumber, new CompFlavorUtility.CompFlavorData(compFlavor));
                    Log.Warning($"activeProcesses were [{CompFlavorUtility.ActiveProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
                }
                else Log.Error($"CompFlavor for input meal into {comp.parent} had a null field, ignoring it. Output meal CompFlavor will be regenerated. Please report.");
            }

            static bool VerifyCompFlavorIntegrity(CompFlavor compFlavor)
            {
                if (compFlavor?.TickCreated == null) return false;
                if (compFlavor?.MealTags == null) return false;
                return true;
            }
        }

        // VEF: retrieve CompFlavor from cache when meal is removed from processor
        public static void HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix(ref Thing outThing, ref Process __instance)
        {
            Log.Warning($"HandleIngredientsAndQualityPostfix for {outThing}");
            if (outThing.TryGetComp(out CompFlavor outCompFlavor))
            {
                int key = __instance.advancedProcessor.parent.thingIDNumber;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(key, out CompFlavorUtility.CompFlavorData cachedCompFlavorData))
                {
                    Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} with iteration {outCompFlavor.Iteration.ToStringSafe()} to cached CompFlavor {cachedCompFlavorData.ToStringSafe()} with processor ID {key.ToStringSafe()} and iteration {cachedCompFlavorData.iteration.ToStringSafe()}");
                    outCompFlavor.MealTags = cachedCompFlavorData.mealTags;
                    outCompFlavor.Iteration = cachedCompFlavorData.iteration;
                    outCompFlavor.CookID = cachedCompFlavorData.cookID;
                    CompFlavorUtility.ActiveProcesses.Remove(key);
                }
            }
        }
    }
}
