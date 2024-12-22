using LudeonTK;
using System.Collections.Generic;
using Verse;

namespace FlavorText;

// adds 4 fields to ThingCategoryDef to store FlavorText information
public class FlavorCategoryModExtension : DefModExtension
{
    public int specificity;  // how many non-duplicate descendant ThingDefs are in this ThingCategoryDef

    public List<string> keywords = []; // keywords to search for when deciding which modded ingredients fit into which ThingCategoryDefs

    public List<string> blacklist = []; // keywords NOT to match; e.g. pig != guinea pig

    public List<ThingCategoryDef> flavorSisterCategories = []; // other Categories that correspond to the FT_Category; usually just 1 but more are supported; FT_ThingCategories are their own separate tree under Root
}
