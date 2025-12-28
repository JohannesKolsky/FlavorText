using HarmonyLib;
using PipeSystem;
using Verse;
using FlavorText;

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

        // VEF: retrieve CompFlavor from cache when meal is removed from processor
        public static void HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix(ref Thing outThing, ref Process __instance)
        {
            Log.Warning($"HandleIngredientsAndQualityPostfix for {outThing}");
            if (outThing.TryGetComp(out CompFlavor outCompFlavor))
            {
                int key = __instance.advancedProcessor.parent.thingIDNumber;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(key, out CompFlavor cachedCompFlavor))
                {
                    Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} to cached CompFlavor {cachedCompFlavor.ToStringSafe()}");
                    outCompFlavor.TickCreated = cachedCompFlavor.TickCreated;
                    outCompFlavor.MealTags = cachedCompFlavor.MealTags;
                    outCompFlavor.IngredientsHitPointPercentage = cachedCompFlavor.IngredientsHitPointPercentage;
                    CompFlavorUtility.ActiveProcesses.Remove(key);
                }
            }
        }
    }
}
