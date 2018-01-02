using System;
using System.Net;
using System.Threading.Tasks;

namespace WebRtcWrapper.Signalling
{
	public interface ISimpleHttpRequest
	{
		Uri Uri { get; }
		WebHeaderCollection Headers { get; }
		string Body { get; }
	}

	public interface ISimpleHttpResponse
	{
		HttpStatusCode Status { get; }
		WebHeaderCollection Headers { get; }
		string Body { get; }
	}

	public interface ISimpleHttpClient
	{
		Task<ISimpleHttpResponse> GetAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> PostAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> DeleteAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> PutAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> OptionsAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> ConnectAsync(ISimpleHttpRequest request);
		Task<ISimpleHttpResponse> TraceAsync(ISimpleHttpRequest request);
	}
}
