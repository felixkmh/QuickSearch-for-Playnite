using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search
{
    // http://www.catalysoft.com/articles/StrikeAMatch.html
    class Matching
    {
        static List<string> GetLetterPairs(in string str)
        {
            var result = new List<string>();
            for(int i = 0; i < str.Length - 1; ++i)
            {
                result.Add(str.Substring(i, 2));
            }
            return result;
        }

        static List<string> GetWordLetterPairs(in string str)
        {
            var result = new List<string>();
            foreach (var word in str.Split(' '))
            {
                foreach(var pair in GetLetterPairs(word))
                {
                    result.Add(pair);
                }
            }
            return result;
        }

        public static double GetScore(in string str1, in string str2)
        {
            var pairs1 = GetWordLetterPairs(RemoveDiacritics(str1));
            var pairs2 = GetWordLetterPairs(RemoveDiacritics(str2));

            int matches = 0;
            for(int i = 0; i < pairs1.Count; ++i)
            {
                for (int j = pairs2.Count - 1; j >= 0; --j)
                {
                    if (pairs1[i].Equals(pairs2[j], StringComparison.OrdinalIgnoreCase))
                    {
                        ++matches;
                        pairs2.RemoveAt(j);
                        break;
                    }
                }
            }
            return (2.0 * matches) / (pairs1.Count + pairs2.Count);
        }

        static string RemoveDiacritics(in string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
