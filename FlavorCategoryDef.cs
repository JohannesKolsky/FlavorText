using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FlavorText;

/// <summary>
/// categories to categorize meal ingredients in a custom system with fine detail vs vanilla
/// also categorizes meals and meal source buildings
/// </summary>
/// 


/// <notes>
/// FT_Foods is the top category because you want to generally allow anything that could be used as an ingredient
/// 
/// </notes>



// TODO: what does [Unsaved] actually do in here?


public class FlavorCategoryDef : Def
{
    // vanilla category that corresponds to the FT_Category
    internal List<ThingCategoryDef> sisterCategories = [];

    // any ThingDefs this FlavorCategoryDef should take in
    // any ThingCategoryDefs whose descendant ThingDefs should be taken in
    internal List<ThingDef> thingDefsToAbsorb = [];
    internal List<ThingCategoryDef> thingCategoryDefsToAbsorb = [];

    // whether the collective inflection is naturally a singular or plural form; e.g. "grilled cabbage" vs "grilled berries"
    // if null, value will be inherited from its category parent
    internal bool? singularCollective = null;

    internal List<string> keywords = []; // keywords to search for when deciding which modded ingredients fit into which FlavorCategoryDefs

    internal List<string> blacklist = []; // keywords NOT to match; e.g. pig != guinea pig

    internal int nestDepth;

    public List<FlavorCategoryDef> parents;

    public List<string> inflectionsOverride = []; // default name for an ingredient when this category is empty (only used if numAllowedMissingIngredients) > 0

    public bool? alwaysUseOverride = null;  // always use the inflection override; used for weird names, like eggs and Brussels sprouts

    [Unsaved]
    public List<FlavorCategoryDef> childCategories = [];
    [Unsaved]
    public List<ThingDef> childThingDefs = [];
    [Unsaved]
    private HashSet<ThingDef> descendantThingDefsCached;

    public IEnumerable<FlavorCategoryDef> ThisAndParents
    {
        get
        {
            yield return this;
            if (!parents.NullOrEmpty())
            {
                foreach (FlavorCategoryDef parent in parents)
                {
                    foreach (FlavorCategoryDef ancestors in parent.ThisAndParents)
                        yield return ancestors;
                }
            }
        }
    }

    public IEnumerable<FlavorCategoryDef> ThisAndDescendants
    {
        get
        {
            FlavorCategoryDef childCategoryDef1 = this;
            yield return childCategoryDef1;
            foreach (FlavorCategoryDef childCategory in childCategoryDef1.childCategories)
            {
                foreach (FlavorCategoryDef childCategoryDef2 in childCategory.ThisAndDescendants)
                    yield return childCategoryDef2;
            }
        }
    }

    public IEnumerable<FlavorCategoryDef> LowestChildCategories
    {
        get
        {
            FlavorCategoryDef childCategoryDef1 = this;
            if (childCategoryDef1.childCategories.Count == 0) yield return childCategoryDef1;
            foreach (FlavorCategoryDef childCategory in childCategoryDef1.childCategories)
            {
                foreach (FlavorCategoryDef childCategoryDef2 in childCategory.LowestChildCategories) yield return childCategoryDef2;
            }

        }
    }

    public HashSet<ThingDef> DescendantThingDefs
    {
        get
        {
            if (descendantThingDefsCached == null)
            {
                foreach (FlavorCategoryDef childCategoryDef in ThisAndDescendants)
                {
                    foreach (ThingDef childThingDef in childCategoryDef.childThingDefs)
                        descendantThingDefsCached.Add(childThingDef);
                }
            }
            return descendantThingDefsCached;
        }
    }


    public bool ContainedInThisOrDescendant(ThingDef thingDef)
    {
        return DescendantThingDefs.Contains(thingDef);
    }

    //FT_Foods -> FT_Fungus -> FT_Morrel

    public bool ContainedInThisOrDescendant(FlavorCategoryDef child)
    {
        return child.ThisAndParents.Contains(this);
    }

    // is this in the list or a descendant of one of the list members?
    public bool DescendantOf(List<FlavorCategoryDef> list)
    {
        foreach (var cat in ThisAndParents)
        {
            if (list.Contains(cat)) return true;
        }
        return false;
    }

    public override void ResolveReferences()
    {
        HashSet<ThingDef> allChildThingDefsCached = [];
        foreach (FlavorCategoryDef childCategoryDef in ThisAndDescendants)
        {
            foreach (ThingDef childThingDef in childCategoryDef.childThingDefs)
                allChildThingDefsCached.Add(childThingDef);
        }
        descendantThingDefsCached = [.. allChildThingDefsCached.Distinct().OrderBy(n => n.label)];
    }

    public static FlavorCategoryDef Named(string defName)
    {
        return DefDatabase<FlavorCategoryDef>.GetNamed(defName);
    }

    public override int GetHashCode()
    {
        return defName.GetHashCode();
    }

    internal static void SetNestLevelRecursive(FlavorCategoryDef cat, int nestDepth)
    {
        nestDepth += 1;
        foreach (FlavorCategoryDef childCategory in cat.childCategories)
        {
            childCategory.nestDepth = nestDepth;
            SetNestLevelRecursive(childCategory, nestDepth + 1);
        }
    }

    public static void FinalizeInit()
    {
        foreach (FlavorCategoryDef allDef in DefDatabase<FlavorCategoryDef>.AllDefs)
        {
            allDef.parents?.ForEach(parent => parent.childCategories.Add(allDef));
        }
        SetNestLevelRecursive(FlavorCategoryDefOf.FT_Root, 0);
    }
}
