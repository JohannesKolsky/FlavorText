using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

//TODO: LookMode.Reference errror for compFlavor
//TODO: saving mid-processing crashes RW

namespace FlavorText
{
	public class CompFlavorUtility : MapComponent
    {
        private static int iterations;
        internal static int Iterations { get => iterations; private set => iterations = value; }

        internal static void Iterate()
        {
            Iterations++;
        }

        private static Dictionary<int, CompFlavorData> activeProcesses;

        private static List<int> processorIDNumbers;
        private static List<CompFlavorData> flavorComps;

        public CompFlavorUtility(Map map) : base(map)
        {
            Iterations = 0;
        }

        internal static void Next()
        {
            Iterations++;
        }

        public static Dictionary<int, CompFlavorData> ActiveProcesses
        {
            get
            {
                activeProcesses ??= [];
                return activeProcesses;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref iterations, "iterations");
            Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Deep, ref processorIDNumbers, ref flavorComps);
            //Log.Warning($"activeProcesses were [{ActiveProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
        }

        public class CompFlavorData : ILoadReferenceable, IExposable
        {
            public int? iteration = null;
            public int? cookID = null;
            public List<string> mealTags = [];

            public CompFlavorData(CompFlavor compFlavor)
            {
                iteration = compFlavor.Iteration;
                cookID = compFlavor.CookID;
                mealTags = compFlavor.MealTags;
            }
            public void ExposeData()
            {
                Scribe_Values.Look(ref iteration, "iteration");
                Scribe_Values.Look(ref cookID, "cookID");
                Scribe_Collections.Look(ref mealTags, "tags", LookMode.Undefined);
            }

            public string GetUniqueLoadID()
            {
                return GetUniqueLoadID();
            }
        }
    }
}
