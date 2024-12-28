using System.Globalization;
using System.Text;

//--TODO: recipe parent hierarchy
//--TODO: spreadsheet descriptions are misaligned

//TODO: blank ingredient option


namespace FlavorText;

public static class Remove  //TODO: add more special chars, like ø
{
    public static string RemoveDiacritics(string stIn)
    {
        string stFormD = stIn.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();

        for (int ich = 0; ich < stFormD.Length; ich++)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(stFormD[ich]);
            }
        }

        return (sb.ToString().Normalize(NormalizationForm.FormC));
    }
}
