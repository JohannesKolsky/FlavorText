using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

//TODO: LookMode.Reference errror for compFlavor
//TODO: saving mid-processing crashes RW
//TODO: cookID isn't being saved
//TODO: ruined soup
//TODO: processor destroyed or uninstalled mid-process
//TDOO: CompFlavorData constructor not found error on load of save

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

        private static Dictionary<string, CompFlavorData> activeProcesses;

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

        public static Dictionary<string, CompFlavorData> ActiveProcesses
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
            Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Deep);
        } 
    }

        public class CompFlavorData : IExposable
        {
            public int? iteration;
            public int? cookID;
            public List<string> mealTags;

            public CompFlavorData(int? iteration, int? cookID, List<string> mealTags)
            {
                this.iteration = iteration.Value;
                this.cookID = cookID.Value;
                this.mealTags = mealTags;
            }

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
        }
}
