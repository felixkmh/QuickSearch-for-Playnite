﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using F23.StringSimilarity;

namespace QuickSearch
{
    public class Matching
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
            var pattern =  RemoveDiacritics(str2.ToLower());
            var input = RemoveDiacritics(str1.ToLower());
            float matchingPairs = MatchingLetterPairs2(input, pattern, ScoreNormalization.Str1);
            var lcs = LongestCommonSubstringDP(input, pattern, ScoreNormalization.Str1);
            float matchingWords = MatchingWords(input, pattern, 0.666f, ScoreNormalization.Str1);
            var w0 = 18f;
            var w1 = 1f;
            var w2 = 1f;
            var score = (w0 * matchingPairs
                + w1 * lcs.Score
                + w2 * matchingWords) / (w0 + w1 + w2);
            return Math.Min(1f, score);
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

        private struct IndexPair
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

            var normalized1 = str1;
            var normalized2 = str2;

            var pairs1 = GetWordLetterPairs2(normalized1);
            var pairs2 = GetWordLetterPairs2(normalized2);

            var pairs1Count = pairs1.Count;
            var pairs2Count = pairs2.Count;

            float matches = 0;
            for (int i = pairs1.Count - 1; i >= 0 && pairs2.Count > 0; --i)
            {
                for (int j = pairs2.Count - 1; j >= 0; --j)
                {
                    if (!pairs1[i].single && !pairs2[j].single)
                    {
                        if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a] && normalized1[pairs1[i].b] == normalized2[pairs2[j].b])
                        {
                            ++matches;
                            pairs2.RemoveAt(j);
                            pairs1.RemoveAt(i);
                            break;
                        }
                    }
                    else if (pairs1[i].single && pairs2[j].single)
                    {
                        if (normalized1[pairs1[i].a] == normalized2[pairs2[j].a])
                        {
                            ++matches;
                            pairs2.RemoveAt(j);
                            pairs1.RemoveAt(i);
                            break;
                        }
                    } 
                }
            }

            var max_matches = Math.Min(pairs1Count, pairs2Count);

            if (pairs2.Count > 0 && pairs1.Count > 0)
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
                    return matches / Math.Max(pairs1Count, pairs2Count);
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
                if (unicodeCategory != UnicodeCategory.NonSpacingMark && unicodeCategory != UnicodeCategory.ModifierSymbol && unicodeCategory != UnicodeCategory.EnclosingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }   

        public static readonly Regex WhiteSpaceLike = new Regex(@"[\s+-]+");

        public static float MatchingWords(in string str1, in string str2, float wordThreshold = 0.6667f, ScoreNormalization normalization = ScoreNormalization.None)
        {
            if (string.IsNullOrWhiteSpace(str1) || string.IsNullOrWhiteSpace(str2))
            {
                return 0f;
            }

            var words1 = WhiteSpaceLike.Split(str1).ToArray();
            var words2 = WhiteSpaceLike.Split(str2).ToList();

            var words1Count = words1.Count();
            var words2Count = words2.Count();

            var matchedPairs = new List<IndexPair>();

            float sum = 0;

            var normalizedLevenshtein = new NormalizedLevenshtein();
            var damerau = new Damerau();

            for (int i = 0; i < words1.Count(); ++i)
            {
                if (words2.Count == 0) break;
                float maxValue = 0;
                int maxIdx = 0;
                for (int j = 0; j < words2.Count; ++j)
                {
                    // var val = (float)words1[i].FuzzyMatch(words2[j]);
                    // var val = FuzzySharp.Fuzz.PartialRatio(words1[i], words2[j]) * 1f / 100f;
                    // var val = MatchingLetterPairs2(words1[i], words2[j], ScoreNormalization.Both);
                    var val = (float)normalizedLevenshtein.Similarity(words1[i], words2[j]);
                    //var maxLength = Math.Max(words1[i].Length, words2[j].Length);
                    //val = (maxLength - val) / maxLength;
                    //val = 1f - val;
                    //val /= (Math.Abs(j - i) / Math.Max(words1Count, words2Count)) + 1;
                    // var val = 1f - ((float)words1[i].LevenshteinDistance(words2[j]) / (float) Math.Max(words1[i].Length, words2[j].Length));
                    if (val > maxValue)
                    {
                        maxValue = val;
                        maxIdx = j;
                    }
                }
                if (maxValue >= wordThreshold)
                {
                    matchedPairs.Add(new IndexPair(i, maxIdx));
                    sum += maxValue;
                    words2.RemoveAt(maxIdx);
                }
            }

            var orderScore = 1f;

            if (matchedPairs.Count > 1)
            {
                int pairsInOrder = 0;
                for (int i = 0; i < matchedPairs.Count - 1; ++i)
                {
                    if (matchedPairs[i].b <= matchedPairs[i + 1].b)
                    {
                        ++pairsInOrder;
                    }
                }

                orderScore = 1f * pairsInOrder / (matchedPairs.Count - 1);
            }


            switch (normalization)
            {
                case ScoreNormalization.None:
                    return sum;
                case ScoreNormalization.Str1:
                    return (float)Math.Pow(sum / words1Count, 1f + (1f - orderScore));
                case ScoreNormalization.Str2:
                    return (float)Math.Pow(sum / words2Count, 1f + (1f - orderScore));
                case ScoreNormalization.Both:
                    return (float)Math.Pow(sum / Math.Max(words1Count, words2Count), 1f + (1f - orderScore));
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
            public int[] PositionsA;
            public int[] PositionsB;
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
            var a = str1;
            var b = str2;

            bool swapped = a.Length < b.Length;

            if (swapped)
            {
                var temp = a;
                a = b;
                b = temp;
            }

            var subStr = string.Empty;

            var matchingPositionsA = new HashSet<int>();
            var matchingPositionsB = new HashSet<int>();

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return new LcsResult() { Score = 0, String = subStr, 
                    PositionsA = matchingPositionsA.OrderBy(i => i).ToArray(), 
                    PositionsB = matchingPositionsB.OrderBy(i => i).ToArray() };

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

            //for(int n = 0; n < a.Length; ++n)
            //{
            //    Debug.Write("[");
            //    for(int m = 0; m < b.Length; ++m)
            //    {
            //        Debug.Write(num[n, m]);
            //        if (m < b.Length - 1)
            //            Debug.Write(", ");
            //    }
            //    Debug.Write("]\n");
            //}

            var y = b.Length - 1;

            while (y >= 0)
            {
                var max = 0;
                var maxColumn = -1;
                for (int x = 0; x < a.Length; ++x)
                {
                    if (num[x, y] > max && !matchingPositionsA.Contains(x))
                    {
                        max = num[x, y];
                        maxColumn = x;
                    }
                }
                if (maxColumn > -1)
                {
                    Enumerable.Range(maxColumn - max + 1, max).ForEach(pos => { matchingPositionsA.Add(pos); });
                    Enumerable.Range(y - max + 1, max).ForEach(pos => { matchingPositionsB.Add(pos); });
                    y -= max;
                } else
                {
                    y -= 1;
                }
            }

            subStr = subStrBuilder.ToString();

            if (swapped)
            {
                var temp = a;
                a = b;
                b = temp;
                var tempSet = matchingPositionsA;
                matchingPositionsA = matchingPositionsB;
                matchingPositionsB = tempSet;
            }

            var result = new LcsResult
            {
                String = subStr,
                Score = subStr.Length,
                Index = b.IndexOf(subStr),
                PositionsA = matchingPositionsA.OrderBy(i => i).ToArray(),
                PositionsB = matchingPositionsB.OrderBy(i => i).ToArray()
            };

            switch (normalization)
            {
                case ScoreNormalization.Str1:
                    result.Score /= a.Length;
                    result.Score /= (b.IndexOf(subStr) * 0.3f) + 1f;
                    break;
                case ScoreNormalization.Str2:
                    result.Score /= b.Length;
                    result.Score /= (a.IndexOf(subStr) * 0.3f) + 1f;
                    break;
                case ScoreNormalization.Both:
                    result.Score /= Math.Min(a.Length, b.Length);
                    result.Score /= (b.IndexOf(subStr) + a.IndexOf(subStr)) * 0.3f + 1f;
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
