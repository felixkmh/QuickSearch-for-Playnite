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

        private MultiFilter(MultiFilter<T> old)
        {
            any.AddRange(old.any);
            all.AddRange(old.all);
        }

        public enum Mode
        {
            None,
            Or,
            And
        }

        public MultiFilter(Func<T, bool> filter)
        {
            any.Add(filter);
        }

        public List<Func<T, bool>> One => any;
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
            return any.Any(f => f(item)) && all.All(f => f(item));
        }
    }
}
