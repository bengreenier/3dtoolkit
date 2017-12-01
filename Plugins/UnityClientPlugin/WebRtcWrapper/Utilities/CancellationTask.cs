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
		
		public static CancellableTask Run(Action action)
		{
			var source = new CancellationTokenSource();

			return new CancellableTask()
			{
				cancellationSource = source,
				Task = Task.Run(action, source.Token)
			};
		}

		public void Cancel()
		{
			this.cancellationSource.Cancel();
		}
	}
}
