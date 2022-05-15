using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch
{
    public class MultiDisposer : IDisposable
    {
        private IEnumerable<IDisposable> disposables;

        public MultiDisposer(IEnumerable<IDisposable> disposables)
        {
            this.disposables = disposables;
        }

        public MultiDisposer(params IEnumerable<IDisposable>[] disposables)
        {
            this.disposables = disposables.SelectMany(d => d);
        }

        public void Dispose()
        {
            foreach(IDisposable disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}
