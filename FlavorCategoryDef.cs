using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FlavorText;

/// <summary>
/// categories to categorize meal ingredients in a custom system with fine detail vs vanilla
/// also categorizes meals and meal source buildings
/// </summary>
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

    public FlavorCategoryDef parent;

    [Unsaved]
    public List<FlavorCategoryDef> childCategories = [];
    [Unsaved]
    public List<ThingDef> childThingDefs = [];
    [Unsaved]
    private HashSet<ThingDef> allChildThingDefsCached;
    [Unsaved]
    private List<ThingDef> sortedChildThingDefsCached;


    public List<ThingDef> SortedChildThingDefs => sortedChildThingDefsCached;

    public IEnumerable<FlavorCategoryDef> Parents
    {
        get
        {
            if (parent != null)
            {
                yield return parent;
                foreach (FlavorCategoryDef grandParent in parent.Parents)
                    yield return grandParent;
            }
        }
    }

    public IEnumerable<FlavorCategoryDef> ThisAndChildCategoryDefs
    {
        get
        {
            FlavorCategoryDef childCategoryDef1 = this;
            yield return childCategoryDef1;
            foreach (FlavorCategoryDef childCategory in childCategoryDef1.childCategories)
            {
                foreach (FlavorCategoryDef childCategoryDef2 in childCategory.ThisAndChildCategoryDefs)
                    yield return childCategoryDef2;
            }
        }
    }

    public IEnumerable<ThingDef> DescendantThingDefs
    {
        get
        {
            foreach (FlavorCategoryDef childCategoryDef in ThisAndChildCategoryDefs)
            {
                foreach (ThingDef childThingDef in childCategoryDef.childThingDefs)
                    yield return childThingDef;
            }
        }
    }


    public bool ContainedInThisOrDescendant(ThingDef thingDef)
    {
        return allChildThingDefsCached.Contains(thingDef);
    }

    public override void ResolveReferences()
    {
        allChildThingDefsCached = [];
        foreach (FlavorCategoryDef childCategoryDef in ThisAndChildCategoryDefs)
        {
            foreach (ThingDef childThingDef in childCategoryDef.childThingDefs)
                allChildThingDefsCached.Add(childThingDef);
        }
        sortedChildThingDefsCached = [.. childThingDefs.OrderBy(n => n.label)];
    }

    public static FlavorCategoryDef Named(string defName)
    {
        return DefDatabase<FlavorCategoryDef>.GetNamed(defName);
    }

    public override int GetHashCode() => defName.GetHashCode();

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
            allDef.parent?.childCategories.Add(allDef);
        }
        SetNestLevelRecursive(Named("FT_Root"), 0);
    }
}
