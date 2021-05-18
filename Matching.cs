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
        public enum ScoreNormalization
        {
            None,
            Str1,
            Str2,
            Both
        }

        static IEnumerable<string> GetLetterPairs(string str)
        {
            for(int i = 0; i < str.Length - 1; ++i)
            {
                yield return str.Substring(i, 2);
            }
            if (str.Length == 1)
            {
                yield return str;
            }
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

        public static float GetCombinedScore(in string str1, in string str2)
        {
            return MatchingLetterPairs(str1, str2, ScoreNormalization.Str1) 
                 + LongestCommonSubstring(str1, str2, ScoreNormalization.Str1).Score 
                 + MatchingWords(str1, str2, 0.7f, ScoreNormalization.Str1);
        }

        public static float MatchingLetterPairs(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            var pairs1 = GetWordLetterPairs(RemoveDiacritics(str1));
            var pairs2 = GetWordLetterPairs(RemoveDiacritics(str2));

            float matches = 0;
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
            switch (normalization)
            {
                case ScoreNormalization.None:
                    return matches;
                case ScoreNormalization.Str1:
                    return matches / pairs1.Count;
                case ScoreNormalization.Str2:
                    return matches / pairs2.Count;
                case ScoreNormalization.Both:
                    return 2 * matches / (pairs1.Count + pairs2.Count);
                default:
                    return matches;
            }
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

        public static float MatchingWords(in string str1, in string str2, float wordThreshold = 0.6667f, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (str1.Length == 0) return 0;
            var words1 = RemoveDiacritics(str1).ToLower().Split(' ').ToList();
            var words2 = RemoveDiacritics(str2).ToLower().Split(' ').ToList();
            float sum = 0;
            for (int i = 0; i < words1.Count; ++i)
            {
                if (words2.Count == 0) break;
                float maxValue = 0;
                int maxIdx = 0;
                for (int j = 0; j < words2.Count; ++j)
                {
                    var val = (float)words1[i].FuzzyMatch(words2[j]);
                    if (val > maxValue)
                    {
                        maxValue = val;
                        maxIdx = j;
                    }
                }
                if (maxValue >= wordThreshold)
                {
                    sum += maxValue;
                    words2.RemoveAt(maxIdx);
                }
            }

            switch (normalization)
            {
                case ScoreNormalization.None:
                    return sum;
                case ScoreNormalization.Str1:
                    return sum / words1.Count;
                case ScoreNormalization.Str2:
                    return sum / words2.Count;
                case ScoreNormalization.Both:
                    return 2 * sum / (words1.Count + words2.Count);
                default:
                    return sum;
            }
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

        public struct LcsResult
        {
            public string String;
            public float Score;
        } 

        public static LcsResult LongestCommonSubstring(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            {
                return new LcsResult { String = string.Empty, Score = 0f};
            }
            var a = RemoveDiacritics(str1.ToLower());
            var b = RemoveDiacritics(str2.ToLower());
            var common = Substrings(a).Intersect(Substrings(b)).OrderByDescending(s => s.Length);
            var lcs = common.FirstOrDefault()??string.Empty;
            var result = new LcsResult { String = lcs, Score = lcs.Length };
            switch (normalization)
            {
                case ScoreNormalization.Str1:
                    result.Score /= str1.Length;
                    break;
                case ScoreNormalization.Str2:
                    result.Score /= str2.Length;
                    break;
                case ScoreNormalization.Both:
                    result.Score /= 0.5f * (str1.Length + str2.Length);
                    break;
            }
            return result;
        }

        public static IEnumerable<string> Substrings(string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                for (int j = i; j < str.Length; ++j)
                {
                    yield return str.Substring(i, j - i + 1);
                }
            }
        }
    }
}
