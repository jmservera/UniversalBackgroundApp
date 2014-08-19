using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace BackgroundTasks
{
    public sealed class AsyncSemaphore:IDisposable
    {
        Semaphore _semaphore;
        public AsyncSemaphore(int initialCount, int maximumCount, string name)
        {
            _semaphore = new Semaphore(initialCount, maximumCount, name);
        }

        public IAsyncOperation<bool> WaitOneAsync()
        {
            return AsyncInfo.Run<bool>(cancellationToken =>
                Task.Run(() =>
                {
                    while (!_semaphore.WaitOne(100))
                    {
                        Logger.Log("Waiting...");
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    return true;
                }, cancellationToken));
        }

        public int Release()
        {
            return _semaphore.Release();
        }

        public int Release(int releaseCount)
        {
            return _semaphore.Release(releaseCount);
        }

        public void Dispose()
        {
            if (_semaphore != null)
            {
                _semaphore.Dispose();
                _semaphore = null;
            }
        }
    }
}
