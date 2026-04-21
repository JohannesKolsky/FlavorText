using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

//DONE: CompFlavorData constructor not found error on load of save
//DONE: error on adding FlavorText while meal is processing, then taking it out when finished
//DONE: saving mid-processing crashes RW
//DONE: processor destroyed or uninstalled mid-process
//--TODO: ruined soup  // disappears
//DONE: LookMode.Reference errror for compFlavor
//DONE: process doesn't get removed when map is destroyed
//DONE: multi-map: need separate CompFlavorUtilities
//DONE: iterations seems to be resetting to 0

namespace FlavorText
{
    public class CompFlavorUtility(Map map) : MapComponent(map)
    {
        private static int iterations = 0;
        internal static int Iterations { get => iterations; private set => iterations = value; }

        internal static void Iterate()
        {
            Iterations++;
        }

        private Dictionary<string, CompFlavorData> activeProcesses = [];
        public Dictionary<string, CompFlavorData> ActiveProcesses { get => activeProcesses; }



        public override void ExposeData()
        {
            Scribe_Values.Look(ref iterations, "iterations");
            Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Deep);
        } 
    }

    public class CompFlavorData : IExposable
    {
    public int? iteration;
    public string cookID;
    public List<string> mealTags;

        public CompFlavorData() { }

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
