
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebRtcWrapper.Signalling;
using WebRtcWrapper.Utilities;
using System.Threading.Tasks;

namespace WebRtcWrapper.UnitTests
{
	[TestClass]
	public class CancellableTaskTests
	{
		[TestMethod]
		public void TaskRuns()
		{
			var value = 0;
			var instance = CancellableTask.Run(() =>
			{
				value += 1;
			});

			instance.Task.Wait();

			Assert.AreEqual(1, value);
		}

		[TestMethod]
		public void TaskCancels()
		{
			var value = 0;
			var instance = CancellableTask.Run(async () =>
			{
				await Task.Delay(5000);
				value += 1;
			});

			instance.Cancel();
			instance.Task.Wait();

			Assert.AreEqual(0, value);
		}
	}
}
