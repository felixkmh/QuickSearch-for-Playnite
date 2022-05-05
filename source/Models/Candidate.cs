using QuickSearch.SearchItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static QuickSearch.Matching;

namespace QuickSearch.Models
{
    public class Candidate : ObservableObject
    {
        public Candidate()
        {

        }

        public Candidate(SearchItems.Candidate candidate)
        {
            Item = candidate.Item;
            Score = candidate.Score;
            Marked = candidate.Marked;
        }

        public ISearchItem<string> Item { get => item; internal set => SetValue(ref item, value); }
        private ISearchItem<string> item;
        public float Score { get => score; internal set => SetValue(ref score, value); }
        private float score;
        //public TextBlock TopLeftFormatted { get; internal set; }
        internal bool Marked;
        private string query;
        public string Query { get => query; internal set => SetValue(ref query, value); }

        public List<Run> GetFormattedRuns(string query)
        {
            var runs = new List<Run>();
            if (Item?.TopLeft != null)
            {
                var topLeft = Item.TopLeft;
                var lcs = LongestCommonSubstringDP(query, topLeft);

                int i = 0;
                while (i < topLeft.Length)
                {
                    int j = i;
                    while (j < topLeft.Length && lcs.PositionsB.Contains(i) == lcs.PositionsB.Contains(j))
                    {
                        ++j;
                    }

                    if (lcs.PositionsB.Contains(i))
                    {
                        Run run = new Run(topLeft.Substring(i, j - i)) { FontWeight = System.Windows.FontWeights.DemiBold };
                        runs.Add(run);
                    }
                    else
                    {
                        runs.Add(new Run(topLeft.Substring(i, j - i)) { FontWeight = System.Windows.FontWeights.Normal });
                    }
                    i += j - i;
                }
            }
            return runs;
        }
    }
}
