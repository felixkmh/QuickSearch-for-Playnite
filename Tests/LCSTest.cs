using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using QuickSearch;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class LCSTest
    {
        [TestMethod]
        public void MatchingPositions()
        {
            {
                var result = Matching.LongestCommonSubstringDP("Firefox Web Browser", "fiwebro", Matching.ScoreNormalization.None);
                if (!Enumerable.SequenceEqual(new int[] { 0, 1, 8, 9, 12, 13, 14 },result.PositionsA))
                {
                    throw new AssertFailedException();
                }
                if (!Enumerable.SequenceEqual(new int[] { 0, 1, 2, 3, 4, 5, 6 }, result.PositionsB))
                {
                    throw new AssertFailedException();
                }
            }
            {
                var result = Matching.LongestCommonSubstringDP("fiwebro", "Firefox Web Browser", Matching.ScoreNormalization.None);
                if (!Enumerable.SequenceEqual(new int[] { 0, 1, 2, 3, 4, 5, 6 }, result.PositionsA))
                {
                    throw new AssertFailedException();
                }
                if (!Enumerable.SequenceEqual(new int[] { 0, 1, 8, 9, 12, 13, 14 }, result.PositionsB))
                {
                    throw new AssertFailedException();
                }
            }
        }
    }
}
