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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Tests
{
    public class PriorityQueueTest
    {
        private CancellationTokenSource tokenGenerator = new CancellationTokenSource();

        private CancellationToken cancellationToken; 

        [Test]
        public void AddItemsTest()
        {
            cancellationToken = tokenGenerator.Token;

            var testQueue = new PriorityBlockingQueue<string>();
            testQueue.EnqueueItem("Unimportant"         , ProcessingPriority.Low);
            testQueue.EnqueueItem("Unimportant"         , ProcessingPriority.Low);
            testQueue.EnqueueItem("Very important"      , ProcessingPriority.High);
            testQueue.EnqueueItem("Somewhat important"  , ProcessingPriority.Medium);

            testQueue.ConsumeItems(cancellationToken).First().Should().Be("Very important");
            testQueue.ConsumeItems(cancellationToken).First().Should().Be("Somewhat important");
            testQueue.ConsumeItems(cancellationToken).First().Should().Be("Unimportant");
        }

        [Test]
        public void CancellationWorksTest()
        {
            cancellationToken = tokenGenerator.Token;

            var testQueue = new PriorityBlockingQueue<string>();
            testQueue.EnqueueItem("Unimportant", ProcessingPriority.Low);
            testQueue.EnqueueItem("Unimportant", ProcessingPriority.Low);
            testQueue.EnqueueItem("Very important", ProcessingPriority.High);
            testQueue.EnqueueItem("Somewhat important", ProcessingPriority.Medium);

            tokenGenerator.Cancel();
            var taskIterator = Task.Run(() => testQueue.ConsumeItems(cancellationToken).Count());
            
            taskIterator.Wait();
            
            //the task hasn't been cancelled and it completed which proves GetItems did exit on cancellation (the only way out)
            taskIterator.IsCompleted.Should().BeTrue();
        }

        [Test]
        public void ContainsTest()
        {
            cancellationToken = tokenGenerator.Token;

            var testQueue = new PriorityBlockingQueue<string>();
            testQueue.EnqueueItem("Unimportant", ProcessingPriority.Low);
            testQueue.EnqueueItem("Unimportant2", ProcessingPriority.Low);

            testQueue.Contains(x=> x == "No such thing").Should().BeFalse();
            testQueue.Contains(x=> x == "Unimportant").Should().BeTrue();
        }
    }
}
