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
    class Matching
    {
        public enum ScoreNormalization
        {
            None,
            Str1,
            Str2,
            Both
        }

        public static float GetCombinedScore(in string str1, in string str2)
        {
            return (6 * MatchingLetterPairs2(str1, str2, ScoreNormalization.Str1)
                  + 2 * LongestCommonSubstringDP(str1, str2, ScoreNormalization.Str1).Score
                  + 1 * MatchingWords(str1, str2, 0.7f, ScoreNormalization.Str1)) / 9f;
            //return Math.Max(
            //    Math.Max(
            //        MatchingLetterPairs(str1, str2, ScoreNormalization.Str1),
            //        LongestCommonSubstringDP(str1, str2, ScoreNormalization.Str1).Score),
            //    MatchingWords(str1, str2, 0.7f, ScoreNormalization.Str1));
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

        // http://www.catalysoft.com/articles/StrikeAMatch.html
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
        // http://www.catalysoft.com/articles/StrikeAMatch.html
        public static float MatchingLetterPairs(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (string.IsNullOrWhiteSpace(str1) || string.IsNullOrWhiteSpace(str2))
            {
                return 0f;
            }

            var pairs1 = GetWordLetterPairs(RemoveDiacritics(str1));
            var pairs2 = GetWordLetterPairs(RemoveDiacritics(str2));

            var pairs1Count = pairs1.Count;
            var pairs2Count = pairs2.Count;

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
                    return matches / pairs1Count;
                case ScoreNormalization.Str2:
                    return matches / pairs2Count;
                case ScoreNormalization.Both:
                    return 2 * matches / (pairs1Count + pairs2Count);
                default:
                    return matches;
            }
        }

        private class IndexPair
        {
            public IndexPair(int a, int b)
            {
                this.a = a;
                this.b = b;
                this.single = false;
            }

            public IndexPair(int a)
            {
                this.a = a;
                this.b = a;
                this.single = true;
            }
            public int a;
            public int b;
            public bool single;
        }

        // http://www.catalysoft.com/articles/StrikeAMatch.html
        static List<IndexPair> GetWordLetterPairs2(in string str)
        {
            var result = new List<IndexPair>();
            if (str.Length == 0)
            {
                return result;
            }
            else if (str.Length == 1)
            {
                result.Add(new IndexPair(0));
            }
            else
            {
                for (int i = 0; i < str.Length; ++i)
                {
                    if (i + 1 < str.Length && str[i] != ' ' && str[i + 1] != ' ')
                    {
                        result.Add(new IndexPair(i, i + 1));
                    }
                    else if (i > 0 && i + 1 < str.Length && str[i - 1] == ' ' && str[i + 1] == ' ' && str[i] != ' ')
                    {
                        result.Add(new IndexPair(i));
                    }
                    else if (i == 0 && str[i] != ' ' && str[i + 1] == ' ')
                    {
                        result.Add(new IndexPair(i));
                    } 
                    else if (i == str.Length - 1 && str[i-1] == ' ' && str[i] != ' ')
                    {
                        result.Add(new IndexPair(i));
                    }
                }
            }

            return result;
        }
        // http://www.catalysoft.com/articles/StrikeAMatch.html
        public static float MatchingLetterPairs2(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (string.IsNullOrWhiteSpace(str1) || string.IsNullOrWhiteSpace(str2))
            {
                return 0f;
            }

            var normalized1 = RemoveDiacritics(str1).ToLower();
            var normalized2 = RemoveDiacritics(str2).ToLower();

            var pairs1 = GetWordLetterPairs2(normalized1);
            var pairs2 = GetWordLetterPairs2(normalized2);

            var pairs1Count = pairs1.Count;
            var pairs2Count = pairs2.Count;

            float matches = 0;
            for (int i = 0; i < pairs1.Count && pairs2.Count > 0; ++i)
            {
                for (int j = pairs2.Count - 1; j >= 0; --j)
                {
                    if (!pairs1[i].single && !pairs2[j].single)
                    {
                        if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a] && normalized1[pairs1[i].b] == normalized2[pairs2[j].b])
                        {
                            ++matches;
                            pairs2.RemoveAt(j);
                            break;
                        }
                    }
                    else if (pairs1[i].single && pairs2[j].single)
                    {
                        if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a])
                        {
                            ++matches;
                            pairs2.RemoveAt(j);
                            break;
                        }
                    } 
                }
            }

            var max_matches = Math.Min(pairs1Count, pairs2Count);

            if (pairs2.Count > 0)
            {
                for (int i = 0; i < pairs1.Count && matches < max_matches && pairs2.Count > 0; ++i)
                {
                    for (int j = pairs2.Count - 1; j >= 0; --j)
                    {
                        if (pairs1[i].single && !pairs2[j].single)
                        {
                            if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a] || normalized1[pairs1[i].a] == normalized2[pairs2[j].b])
                            {
                                matches += 0.5f;
                                pairs2.RemoveAt(j);
                                break;
                            }
                        }
                        else if (!pairs1[i].single && pairs2[j].single)
                        {
                            if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a] || normalized1[pairs1[i].b] == normalized2[pairs2[j].a])
                            {
                                matches += 0.5f;
                                pairs2.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }

            switch (normalization)
            {
                case ScoreNormalization.None:
                    return matches;
                case ScoreNormalization.Str1:
                    return matches / pairs1Count;
                case ScoreNormalization.Str2:
                    return matches / pairs2Count;
                case ScoreNormalization.Both:
                    return 2 * matches / (pairs1Count + pairs2Count);
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
            if (string.IsNullOrWhiteSpace(str1) || string.IsNullOrWhiteSpace(str2))
            {
                return 0f;
            }

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
                    // var val = (float)words1[i].FuzzyMatch(words2[j]);
                    // var val = FuzzySharp.Fuzz.PartialRatio(words1[i], words2[j]) * 1f / 100f;
                    var val = MatchingLetterPairs2(words1[i], words2[j], ScoreNormalization.Str2);
                    // var val = 1f - ((float)words1[i].LevenshteinDistance(words2[j]) / (float) Math.Max(words1[i].Length, words2[j].Length));
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

        private static readonly Regex regex = new Regex("[" + Regex.Escape("&.,:;^°_`´~+!\"§$% &/()=?<>#|'’") + "\\-]");

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
            public int Index;
        } 

        public static LcsResult LongestCommonSubstring(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (string.IsNullOrWhiteSpace(str1) || string.IsNullOrWhiteSpace(str2))
            {
                return new LcsResult { String = string.Empty, Score = 0f};
            }
            var a = RemoveDiacritics(str1.ToLower());
            var b = RemoveDiacritics(str2.ToLower());
            var stringsA = Substrings(a);
            var stringsB = Substrings(b);
            var common = stringsA.Intersect(stringsB).OrderByDescending(s => s.Length);
            var lcs = common.FirstOrDefault() ?? string.Empty;
            var result = new LcsResult { String = lcs, Score = lcs.Length };
            switch (normalization)
            {
                case ScoreNormalization.Str1:
                    result.Score /= str1.Length + b.IndexOf(lcs);
                    break;
                case ScoreNormalization.Str2:
                    result.Score /= str2.Length + a.IndexOf(lcs);
                    break;
                case ScoreNormalization.Both:
                    result.Score /= 0.5f * (str1.Length + str2.Length + a.IndexOf(lcs) + b.IndexOf(lcs));
                    break;
            }
            return result;
        }

        // see https://www.programmingalgorithms.com/algorithm/longest-common-substring/
        public static LcsResult LongestCommonSubstringDP(in string str1, in string str2, ScoreNormalization normalization = ScoreNormalization.None)
        {
            var a = RemoveDiacritics(str1.ToLower());
            var b = RemoveDiacritics(str2.ToLower());

            var subStr = string.Empty;

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return new LcsResult() { Score = 0, String = subStr };

            int[,] num = new int[a.Length, b.Length];
            int maxlen = 0;
            int lastSubsBegin = 0;
            StringBuilder subStrBuilder = new StringBuilder();

            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (a[i] != b[j])
                    {
                        num[i, j] = 0;
                    }
                    else
                    {
                        if ((i == 0) || (j == 0))
                            num[i, j] = 1;
                        else
                            num[i, j] = 1 + num[i - 1, j - 1];

                        if (num[i, j] > maxlen)
                        {
                            maxlen = num[i, j];

                            int thisSubsBegin = i - num[i, j] + 1;

                            if (lastSubsBegin == thisSubsBegin)
                            {
                                subStrBuilder.Append(a[i]);
                            }
                            else
                            {
                                lastSubsBegin = thisSubsBegin;
                                subStrBuilder.Length = 0;
                                subStrBuilder.Append(a.Substring(lastSubsBegin, (i + 1) - lastSubsBegin));
                            }
                        }
                    }
                }
            }

            subStr = subStrBuilder.ToString();

            var result = new LcsResult { String = subStr, Score = subStr.Length, Index = b.IndexOf(subStr) };

            switch (normalization)
            {
                case ScoreNormalization.Str1:
                    result.Score /= a.Length + b.IndexOf(subStr);
                    break;
                case ScoreNormalization.Str2:
                    result.Score /= b.Length + a.IndexOf(subStr);
                    break;
                case ScoreNormalization.Both:
                    result.Score /= 0.5f * (a.Length + b.Length + a.IndexOf(subStr) + b.IndexOf(subStr));
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
