//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Model
{
    public class PriorityBlockingQueue<T> : IPriorityQueue<T>, IDisposable
    {
        private const int MAX_BLOCKING_BETWEEN_CHECKS = 20000;

        private readonly ConcurrentQueue<T> _highPriorityQueue = new ConcurrentQueue<T>();
        private readonly ConcurrentQueue<T> _mediumPriorityQueue = new ConcurrentQueue<T>();
        private readonly ConcurrentQueue<T> _lowPriorityQueue = new ConcurrentQueue<T>();
        private readonly AutoResetEvent _blockingConstruct = new AutoResetEvent(false);

        public void EnqueueItem(T item, ProcessingPriority priority)
        {
            switch (priority)
            {
                case ProcessingPriority.High:
                    _highPriorityQueue.Enqueue(item);
                    break;

                case ProcessingPriority.Medium:
                    _mediumPriorityQueue.Enqueue(item);
                    break;

                default:
                    _lowPriorityQueue.Enqueue(item);
                    break;
            }

            _blockingConstruct.Set();
        }


        public IEnumerable<T> ConsumeItems(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                T item = default(T);

                //iterate over all items from the high priority queue before proceeding to the next queue
                if (!_highPriorityQueue.IsEmpty)
                {
                    _highPriorityQueue.TryDequeue(out item);
                    yield return item;
                    continue;
                }

                if (!_mediumPriorityQueue.IsEmpty)
                {
                    _mediumPriorityQueue.TryDequeue(out item);
                    yield return item;
                    continue;
                }

                if (!_lowPriorityQueue.IsEmpty)
                {
                    _lowPriorityQueue.TryDequeue(out item);
                    yield return item;
                }
                else
                {
                    _blockingConstruct.WaitOne(MAX_BLOCKING_BETWEEN_CHECKS);
                }

            }
        }

        public bool Contains(Func<T, bool> predicate)
        {
            return
                _lowPriorityQueue.Any(predicate) || _mediumPriorityQueue.Any(predicate) || _highPriorityQueue.Any(predicate);
        }

        public int Count
        {
            get { return _highPriorityQueue.Count + _mediumPriorityQueue.Count + _lowPriorityQueue.Count; }
        }

        public void Dispose()
        {
            _blockingConstruct.Dispose();
        }

        public override string ToString()
        {
            return
                string.Format("PriorityQueue items High: {0} \tMedium: {1} \tLow: {2}", _highPriorityQueue.Count,_mediumPriorityQueue.Count, _lowPriorityQueue.Count);
        }
    }
}
