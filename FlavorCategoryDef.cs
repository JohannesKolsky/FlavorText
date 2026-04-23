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
    public List<ThingCategoryDef> sisterCategories = [];

    // any ThingDefs this FlavorCategoryDef should take in
    // any ThingCategoryDefs whose descendant ThingDefs should be taken in
    public List<ThingDef> thingDefsToAbsorb = [];
    public List<ThingCategoryDef> thingCategoryDefsToAbsorb = [];

    // whether the collective inflection is naturally a singular or plural form; e.g. "grilled cabbage" vs "grilled berries"
    // if null, value will be inherited from its category parent
    public bool? singularCollective;

    public List<string> keywords = []; // keywords to search for when deciding which modded ingredients fit into which FlavorCategoryDefs

    public List<string> blacklist = []; // keywords NOT to match; e.g. pig != guinea pig

    public List<string> blacklistedMods = [];  // mods whose ThingDefs should not be added to this category by the auto-categorizer; thingDefsToAbsorb and thingCategoryDefsToAbsorb will bypass this


    public List<FlavorCategoryDef> parents;

    public List<string> inflectionsOverride; // default name for an ingredient when this category is empty (only used if numAllowedMissingIngredients) > 0

    public bool? alwaysUseOverride;  // always use the inflection override; used for weird names, like eggs and Brussels sprouts

    internal int nestDepth; 

    [Unsaved]
    private List<FlavorCategoryDef> childCategories = [];
    [Unsaved]
    private List<ThingDef> childThingDefs = [];
    [Unsaved]
    private HashSet<ThingDef> descendantThingDefsCached;

    internal List<ThingCategoryDef> SisterCategories { get => sisterCategories; set => sisterCategories = value; }
    internal List<ThingDef> ThingDefsToAbsorb { get => thingDefsToAbsorb; set => thingDefsToAbsorb = value; }
    internal bool? SingularCollective { get => singularCollective; set => singularCollective = value; }
    internal List<string> Keywords { get => keywords; set => keywords = value; }
    internal List<string> BlacklistedMods { get => blacklistedMods; set => blacklistedMods = value; }
    internal List<FlavorCategoryDef> Parents { get => parents; set => parents = value; }
    internal List<string> InflectionsOverride { get => inflectionsOverride; set => inflectionsOverride = value; }
    internal bool? AlwaysUseOverride { get => alwaysUseOverride; set => alwaysUseOverride = value; }
    internal List<FlavorCategoryDef> ChildCategories { get => childCategories; set => childCategories = value; }
    internal List<ThingDef> ChildThingDefs { get => childThingDefs; set => childThingDefs = value; }

    internal IEnumerable<FlavorCategoryDef> ThisAndAncestors
    {
        get
        {
            yield return this;
            if (!Parents.NullOrEmpty())
            {
                foreach (FlavorCategoryDef parent in Parents)
                {
                    foreach (FlavorCategoryDef ancestors in parent.ThisAndAncestors)
                        yield return ancestors;
                }
            }
        }
    }

    internal IEnumerable<FlavorCategoryDef> AncestorCategories
    {
        get
        {
            if (!Parents.NullOrEmpty())
            {
                foreach (FlavorCategoryDef parent in Parents)
                {
                    foreach (FlavorCategoryDef ancestors in parent.ThisAndAncestors)
                        yield return ancestors;
                }
            }
        }
    }

    internal IEnumerable<FlavorCategoryDef> ThisAndDescendants
    {
        get
        {
            FlavorCategoryDef origin = this;
            yield return origin;
            foreach (FlavorCategoryDef childCategory1 in origin.ChildCategories)
            {
                foreach (FlavorCategoryDef childCategory2 in childCategory1.ThisAndDescendants)
                    yield return childCategory2;
            }
        }
    }
    internal IEnumerable<FlavorCategoryDef> DescendantCategories
    {
        get
        {
            foreach (FlavorCategoryDef childCategory1 in ChildCategories)
            {
                foreach (FlavorCategoryDef childCategory2 in childCategory1.ThisAndDescendants)
                    yield return childCategory2;
            }
        }
    }

    internal IEnumerable<FlavorCategoryDef> LowestChildCategories
    {
        get
        {
            FlavorCategoryDef childCategoryDef1 = this;
            if (childCategoryDef1.ChildCategories.Count == 0) yield return childCategoryDef1;
            foreach (FlavorCategoryDef childCategory in childCategoryDef1.ChildCategories)
            {
                foreach (FlavorCategoryDef childCategoryDef2 in childCategory.LowestChildCategories) yield return childCategoryDef2;
            }

        }
    }

    internal HashSet<ThingDef> DescendantThingDefs
    {
        get
        {
            if (descendantThingDefsCached == null)
            {
                descendantThingDefsCached = [];
                foreach (FlavorCategoryDef childCategoryDef in ThisAndDescendants)
                {
                    foreach (ThingDef childThingDef in childCategoryDef.ChildThingDefs)
                        descendantThingDefsCached.Add(childThingDef);
                }
            }
            return descendantThingDefsCached;
        }
    }


    internal bool ContainedInThisOrDescendant(ThingDef thingDef)
    {
        return DescendantThingDefs.Contains(thingDef);
    }

    //FT_Foods -> FT_Fungus -> FT_Morrel

    internal bool ContainedInThisOrDescendant(FlavorCategoryDef child)
    {
        return child.ThisAndAncestors.Contains(this);
    }

    // is this in the list or a descendant of one of the list members?
    internal bool DescendantOf(List<FlavorCategoryDef> list)
    {
        foreach (var cat in ThisAndAncestors)
        {
            if (list.Contains(cat)) return true;
        }
        return false;
    }

    internal static FlavorCategoryDef Named(string defName)
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
        foreach (FlavorCategoryDef childCategory in cat.ChildCategories)
        {
            childCategory.nestDepth = nestDepth;
            SetNestLevelRecursive(childCategory, nestDepth + 1);
        }
    }

    internal static void FinalizeInit()
    {
        foreach (FlavorCategoryDef allDef in DefDatabase<FlavorCategoryDef>.AllDefs)
        {
            allDef.Parents?.ForEach(parent => parent.ChildCategories.Add(allDef));
        }
        SetNestLevelRecursive(FlavorCategoryDefOf.FT_Root, 0);
    }
}
