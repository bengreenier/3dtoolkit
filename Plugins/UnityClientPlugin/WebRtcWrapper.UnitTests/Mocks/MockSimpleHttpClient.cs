using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRtcWrapper.Signalling;

namespace WebRtcWrapper.UnitTests.Mocks
{
	public class MockSimpleHttpClient : ISimpleHttpClient
	{
		public Task<ISimpleHttpResponse> ConnectAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> DeleteAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> GetAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> OptionsAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> PostAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> PutAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<ISimpleHttpResponse> TraceAsync(ISimpleHttpRequest request)
		{
			throw new NotImplementedException();
		}
	}
}
