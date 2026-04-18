using FlavorText;
using HarmonyLib;
using ProcessorFramework;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?

namespace SYR
{
    /// <summary>
    /// patches for processor buildings from SYR Processor Framework
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_SYR
    {
        static HarmonyPatches_SYR()
        {
            var patchType = typeof(HarmonyPatches_SYR);
            Harmony harmony = new("rimworld.hekmo.SYR");
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ProcessorFramework.CompProcessor"), "TakeOutProduct"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_TakeOutProductPrefix"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_TakeOutProductPostfix"));
            }
        }

        // cache CompFlavor before old meal is destroyed
        public static void HarmonyPatch_SYR_TakeOutProductPrefix(ref ActiveProcess activeProcess, ref ThingComp __instance)
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

        // retrieve cached CompFlavor and apply it to new meal
        public static void HarmonyPatch_SYR_TakeOutProductPostfix(ref ThingComp __instance, ref Thing __result)
        {
            if (__result.TryGetComp(out CompFlavor outCompFlavor))
            {
                int key = __instance.parent.thingIDNumber;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(key, out CompFlavor cachedCompFlavor))
                {
                    outCompFlavor.TickCreated = cachedCompFlavor.TickCreated;
                    outCompFlavor.MealTags = cachedCompFlavor.MealTags;
                    //outCompFlavor.IngredientsHitPointPercentage = cachedCompFlavor.IngredientsHitPointPercentage;
                    CompFlavorUtility.ActiveProcesses.Remove(key);
                }
            }
        }
    }
}
