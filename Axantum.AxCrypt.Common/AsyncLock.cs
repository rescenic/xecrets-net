﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Axantum.AxCrypt.Common
{
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private readonly Task<IDisposable> _releaser;

        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync()
        {
            Task wait = _semaphore.WaitAsync();
            if (wait.IsCompleted)
            {
                return _releaser;
            }
            return wait.ContinueWith(
                (task, state) =>
                {
                    return (IDisposable)state;
                },
                _releaser.Result, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly AsyncLock m_toRelease;

            internal Releaser(AsyncLock toRelease)
            {
                m_toRelease = toRelease;
            }

            public void Dispose()
            {
                m_toRelease._semaphore.Release();
            }
        }
    }
}