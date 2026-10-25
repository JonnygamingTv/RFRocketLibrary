using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RFRocketLibrary.Models;
using Xunit;

namespace RFRocketLibrary.Tests
{
    public class SerialQueueTests
    {
        [Fact]
        public async Task Enqueue_RunsActionsInOrder()
        {
            var queue = new SerialQueue();
            var order = new List<int>();

            var tasks = new List<Task>();
            for (var i = 0; i < 64; i++)
            {
                var captured = i;
                tasks.Add(queue.Enqueue(() => order.Add(captured))!);
            }

            await Task.WhenAll(tasks);

            Assert.Equal(64, order.Count);
            for (var i = 0; i < 64; i++)
                Assert.Equal(i, order[i]);
        }

        [Fact]
        public async Task Enqueue_FailingTask_DoesNotBreakTheChain()
        {
            var queue = new SerialQueue();

            var first = queue.Enqueue((Func<int>) (() => throw new InvalidOperationException("boom")));
            var second = queue.Enqueue(() => 42);

            await Assert.ThrowsAsync<InvalidOperationException>(() => first!);
            Assert.Equal(42, await second!);
        }

        [Fact]
        public async Task Enqueue_AsyncActions_RunSequentially()
        {
            var queue = new SerialQueue();
            var events = new List<string>();

            var t1 = queue.Enqueue(async () =>
            {
                await Task.Delay(20);
                events.Add("first");
            });
            var t2 = queue.Enqueue(async () =>
            {
                // Even though this delay is shorter, the queue must serialize execution.
                await Task.Delay(1);
                events.Add("second");
            });

            await Task.WhenAll(t1!, t2!);

            Assert.Equal(new[] {"first", "second"}, events);
        }
    }
}
