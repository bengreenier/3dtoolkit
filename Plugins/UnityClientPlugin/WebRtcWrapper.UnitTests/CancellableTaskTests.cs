
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebRtcWrapper.Signalling;
using WebRtcWrapper.Utilities;
using System.Threading.Tasks;
using System.Threading;

namespace WebRtcWrapper.UnitTests
{
	[TestClass]
	public class CancellableTaskTests
	{
		[TestMethod]
		public void CancellableTask_TaskRuns()
		{
			var value = 0;
			var instance = CancellableTask.Run((CancellationToken token) =>
			{
				value += 1;
			});

			instance.Task.Wait();

			Assert.AreEqual(1, value);
		}

		[TestMethod]
		public void CancellableTask_TaskCancels()
		{
			var value = 0;
			var instance = CancellableTask.Run(async (CancellationToken token) =>
			{
				await Task.Delay(5000);
				value += 1;
			});

			instance.Cancel();

			Assert.AreEqual(0, value);
		}

		[TestMethod]
		public void CancellableTask_HangingTaskCancels()
		{
			var value = 0;
			var instance = CancellableTask.Run(async (CancellationToken token) =>
			{
				while (!token.IsCancellationRequested)
				{
					await Task.Delay(5000);
					value += 1;
				}
			});

			instance.Cancel();

			Assert.AreEqual(0, value);
		}
	}
}
