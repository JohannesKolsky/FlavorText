using System.Collections.Generic;
using Verse;

namespace FlavorText;

// adds 4 fields to ThingCategoryDef to store FlavorText information
public class FlavorCategoryModExtension : DefModExtension
{

    internal List<ThingCategoryDef> FlavorSisterCategories = []; // other Categories that correspond to the FT_Category; usually just 1 but more are supported; FT_ThingCategories are their own separate tree under Root

    internal int Specificity;  // how many non-duplicate descendant ThingDefs are in this ThingCategoryDef

    internal bool? SingularCollective = null;  // whether the collective inflection is naturally a singular or plural form; e.g. "grilled cabbage" vs "grilled berries"

    internal List<string> Keywords = []; // keywords to search for when deciding which modded ingredients fit into which ThingCategoryDefs

    internal List<string> Blacklist = []; // keywords NOT to match; e.g. pig != guinea pig
}
