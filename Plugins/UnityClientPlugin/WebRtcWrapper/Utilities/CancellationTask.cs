using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebRtcWrapper.Utilities
{
	public class CancellableTask
	{
		private CancellationTokenSource cancellationSource;
		public Task Task
		{
			get;
			private set;
		}
		
		public static CancellableTask Run(Action<CancellationToken> action)
		{
			var source = new CancellationTokenSource();

			var ct = new CancellableTask()
			{
				cancellationSource = source,
			};

			ct.Task = Task.Run(() => action(source.Token), source.Token);

			return ct;
		}

		public void Cancel(bool skipWait = false)
		{
			this.cancellationSource.Cancel();

			if (!skipWait)
			{
				try
				{
					this.Task.Wait();
				}
				catch (AggregateException ex)
				{
					var remainingAe = new List<Exception>();
					foreach (var e in ex.InnerExceptions)
					{
						if (e.GetType() != typeof(TaskCanceledException))
						{
							remainingAe.Add(e);
						}
					}

					if (remainingAe.Count > 0)
					{
						throw new AggregateException(remainingAe);
					}
				}
			}
		}
	}
}
