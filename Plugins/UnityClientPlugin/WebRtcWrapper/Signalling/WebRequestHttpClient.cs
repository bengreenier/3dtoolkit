using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace WebRtcWrapper.Signalling
{
	public class WebRequestHttpClient : ISimpleHttpClient
	{
		public class ClientException : SimpleHttpClientException
		{
			public ClientException(Exception inner) : base(inner) { }
		}

		private class Response : ISimpleHttpResponse
		{
			public HttpStatusCode Status { get; set; }

			public WebHeaderCollection Headers { get; set; }

			public string Body { get; set; }
		}

		public async Task<ISimpleHttpResponse> ConnectAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "CONNECT");
		}

		public async Task<ISimpleHttpResponse> DeleteAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "DELETE");
		}

		public async Task<ISimpleHttpResponse> GetAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "GET");
		}

		public async Task<ISimpleHttpResponse> OptionsAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "OPTIONS");
		}

		public async Task<ISimpleHttpResponse> PostAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "POST");
		}

		public async Task<ISimpleHttpResponse> PutAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "PUT");
		}

		public async Task<ISimpleHttpResponse> TraceAsync(ISimpleHttpRequest request)
		{
			return await this.IssueRequest(request, "TRACE");
		}

		private async Task<ISimpleHttpResponse> IssueRequest(ISimpleHttpRequest request, string method)
		{
			try
			{
				var req = (HttpWebRequest)WebRequest.Create(request.Uri);

				req.Headers = request.Headers;
				req.Method = method.ToUpper();

				if (!string.IsNullOrEmpty(request.Body))
				{
					var reqStream = await req.GetRequestStreamAsync();

					using (var sw = new StreamWriter(reqStream))
					{
						sw.Write(request.Body);
					}
				}

				var res = (HttpWebResponse)await req.GetResponseAsync();

				var resStream = res.GetResponseStream();

				string resBody = null;
				using (var sr = new StreamReader(resStream))
				{
					resBody = sr.ReadToEnd();
				}

				return new Response()
				{
					Status = res.StatusCode,
					Headers = res.Headers,
					Body = resBody
				};
			}
			catch (Exception ex)
			{
				throw new ClientException(ex);
			}
		}
	}
}
