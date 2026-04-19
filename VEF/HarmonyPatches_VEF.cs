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
            var patchType = typeof(HarmonyPatches_VEF);
            Harmony harmony = new("rimworld.hekmo.VEF");
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.AdvancedProcessorsManager"), "AddIngredient"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_AddIngredientPrefix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.Process"), "HandleIngredientsAndQuality"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.CompAdvancedResourceProcessor"), "PostDeSpawn"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_PostDeSpawnPostfix"));
            }
        }

        // VEF: cache CompFlavor when meal is added to processor
        public static void HarmonyPatch_VEF_AddIngredientPrefix(ref ThingComp comp, ref Thing thing)
        {
            if (thing.TryGetComp(out CompFlavor compFlavor))
            {
                if (VerifyCompFlavorIntegrity(compFlavor))
                {
                    CompFlavorUtility.ActiveProcesses.Add(comp.parent.ThingID, new CompFlavorData(compFlavor));
                    Log.Warning($"activeProcesses were [{CompFlavorUtility.ActiveProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
                }
                else Log.Error($"CompFlavor for input meal into {comp.parent} had a null field, ignoring it. Output meal CompFlavor will be regenerated. Please report.");
            }

            static bool VerifyCompFlavorIntegrity(CompFlavor compFlavor)
            {
                if (compFlavor?.Iteration == null) return false;
                // no need for cookID since that might be null for spawned meals
                if (compFlavor?.MealTags == null) return false;
                return true;
            }
        }

        // VEF: retrieve CompFlavor from cache when meal is removed from processor
        public static void HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix(ref Thing outThing, ref Process __instance)
        {
            if (outThing.TryGetComp(out CompFlavor outCompFlavor))
            {
                string processorID = __instance.advancedProcessor.parent.ThingID;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(processorID, out CompFlavorData cachedCompFlavorData))
                {
                    Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} with iteration {outCompFlavor.Iteration.ToStringSafe()} to cached CompFlavor {cachedCompFlavorData.ToStringSafe()} with processor ID {processorID.ToStringSafe()} and iteration {cachedCompFlavorData.iteration.ToStringSafe()}");
                    outCompFlavor.Iteration = cachedCompFlavorData.iteration;
                    outCompFlavor.CookID = cachedCompFlavorData.cookID;
                    outCompFlavor.MealTags = cachedCompFlavorData.mealTags;
                    CompFlavorUtility.ActiveProcesses.Remove(processorID);
                }
            }
        }

        // VEF: remove item from CompFlavorUtility if the processor despawns
        public static void HarmonyPatch_VEF_PostDeSpawnPostfix(ref CompAdvancedResourceProcessor __instance)
        {
            CompFlavorUtility.ActiveProcesses.Remove(__instance.parent.ThingID);
            Log.Warning($"after removal, activeProcesses were [{CompFlavorUtility.ActiveProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
        }
    }
}
