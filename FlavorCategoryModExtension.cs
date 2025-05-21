using System.Collections.Generic;
using Verse;

namespace FlavorText;

// adds 4 fields to ThingCategoryDef to store FlavorText information
public class FlavorCategoryModExtension : DefModExtension
{
    // vanilla category that corresponds to the FT_Category
    // FT_ThingCategories are their own separate tree under Root
    internal ThingCategoryDef VanillaSisterCategory;

    // other categories whose immediate child thingDefs should be incorporated into this FT_Category
    internal List<ThingCategoryDef> CategoriesToAbsorb = [];

    
    // whether the collective inflection is naturally a singular or plural form; e.g. "grilled cabbage" vs "grilled berries"
    // if null, value will be inherited from the parent
    internal bool? SingularCollective = null;  

    internal List<string> Keywords = []; // keywords to search for when deciding which modded ingredients fit into which ThingCategoryDefs

    internal List<string> Blacklist = []; // keywords NOT to match; e.g. pig != guinea pig
}
