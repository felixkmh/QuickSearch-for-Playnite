using DuoVia.FuzzyStrings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuickSearch
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
            if (str.Length == 1)
            {
                result.Add(str);
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
            return matches;
        }

        public static double GetScoreNormalized(in string str1, in string str2)
        {
            if (str1.Length == 0) return 0;
            var pairs1 = GetWordLetterPairs(RemoveDiacritics(str1));
            var pairs2 = GetWordLetterPairs(RemoveDiacritics(str2));

            int matches = 0;
            for (int i = 0; i < pairs1.Count; ++i)
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

        public static string RemoveDiacritics(in string text)
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

        public static double MatchingWords(in string input, in string str)
        {
            if (input.Length == 0) return 0;
            var words1 = RemoveDiacritics(input).ToLower().Split(' ').ToList();
            var words2 = RemoveDiacritics(str).ToLower().Split(' ').ToList();
            double sum = 0;
            for (int i = 0; i < words1.Count; ++i)
            {
                if (words2.Count == 0) break;
                double maxValue = 0;
                int maxIdx = 0;
                for (int j = 0; j < words2.Count; ++j)
                {
                    var val = words1[i].FuzzyMatch(words2[j]);
                    if (val > maxValue)
                    {
                        maxValue = val;
                        maxIdx = j;
                    }
                }
                sum += maxValue >= 0.75 ? maxValue : 0;
                if (maxValue >= 0.75)
                    words2.RemoveAt(maxIdx);
            }
            return sum;
        }

        private static Regex regex = new Regex("[" + Regex.Escape("&.,:;^°_`´~+!\"§$% &/()=?<>#|'’") + "\\-]");

        public static string RemoveSpecial(in string input)
        {
            var stringBuilder = new StringBuilder();
            foreach (var c in input)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.OtherSymbol)
                {
                    stringBuilder.Append(c);
                }
            }
            return regex.Replace(stringBuilder.ToString(), "");
        }
    }
}
