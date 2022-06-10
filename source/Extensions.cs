using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch
{
    public static class Extensions
    {
        public static string ToHtml(this System.Windows.Media.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static int FindSortedIndex<T>(this IList<T> list, T item, Comparison<T> comparison)
        {
            if (comparison(list.First(), item) == 1) return 0;
            if (comparison(list.Last(), item) == -1) return list.Count;
            int start = 0;
            int end = list.Count - 1;
            int currentIndex = (end + start) / 2;
            while(start <= end)
            {
                currentIndex = (end + start) / 2;
                T currentItem = list[currentIndex];
                var c = comparison(currentItem, item);
                if (c == 0)
                {
                    break;
                }
                if (c < 0)
                {
                    start = currentIndex + 1;
                }
                if (c > 0)
                {
                    end = currentIndex - 1;
                }
            }
            return currentIndex;
        }

        public static void InsertSorted<T>(this IList<T> list, T item, Comparison<T> comparison)
        {
            int insertionIndex = list.FindSortedIndex(item, comparison);
            if (insertionIndex < list.Count)
            {
                list.Insert(insertionIndex, item);
            } else
            {
                list.Add(item);
            }
        }

        public static void InsertRangeSorted<T>(this IList<T> list, IEnumerable<T> items, Comparison<T> comparison)
        {
            foreach(T item in items)
            {
                list.InsertSorted(item, comparison);
            }
        }
    }
}
