using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch
{
    public class MultiFilter<T>
    {
        private readonly List<Func<T, bool>> any = new List<Func<T, bool>>();
        private readonly List<Func<T, bool>> all = new List<Func<T, bool>>();

        public MultiFilter() { }

        private MultiFilter(MultiFilter<T> old)
        {
            if (old != null)
            {
                any.AddRange(old.any);
                all.AddRange(old.all);
            }
        }

        public MultiFilter(Func<T, bool> filter, MultiFilter<T> old = null, Mode mode = Mode.Or) : this(old)
        {
            switch (mode)
            {
                case Mode.Or:
                    any.Add(filter);
                    break;
                case Mode.And:
                    all.Add(filter);
                    break;
                default:
                    break;
            }
        }

        public enum Mode
        {
            None,
            Or,
            And
        }

        public List<Func<T, bool>> Any => any;
        public List<Func<T, bool>> All => all;

        public MultiFilter<T> And(Func<T, bool> filter)
        {
            all.Add(filter);
            return this;
        }

        public MultiFilter<T> Or(Func<T, bool> filter)
        {
            any.Add(filter);
            return this;
        }

        public MultiFilter<T> CopyAndAdd(Func<T, bool> filter, Mode mode = Mode.Or)
        {
            switch (mode)
            {
                case Mode.Or:
                    return new MultiFilter<T>(this).Or(filter);
                case Mode.And:
                    return new MultiFilter<T>(this).And(filter);
                default:
                    return new MultiFilter<T>(this);
            }
        }

        public bool Eval(T item)
        {
            return IsEmpty || (any.Any(f => f(item)) && all.All(f => f(item)));
        }

        public bool IsEmpty => any.Count == 0 && all.Count == 0;
    }
}
