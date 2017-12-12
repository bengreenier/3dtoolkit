
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HyperMock;
using WebRtcWrapper.Signalling;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace WebRtcWrapper.UnitTests
{
    [TestClass]
    public class Signalling2Tests
    {
		public class ThreadsafeWrappedClient : ISimpleHttpClient
		{
			private ISimpleHttpClient wrapped;

			public ThreadsafeWrappedClient(ISimpleHttpClient wrapped)
			{
				this.wrapped = wrapped;
			}

			private object connectLock = new object();
			public Task<ISimpleHttpResponse> ConnectAsync(ISimpleHttpRequest request)
			{
				lock (connectLock)
				{
					return this.wrapped.ConnectAsync(request);
				}
			}

			private object deleteLock = new object();
			public Task<ISimpleHttpResponse> DeleteAsync(ISimpleHttpRequest request)
			{
				lock (deleteLock)
				{
					return this.wrapped.DeleteAsync(request);
				}
			}

			private object getLock = new object();
			public Task<ISimpleHttpResponse> GetAsync(ISimpleHttpRequest request)
			{
				lock (getLock)
				{
					return this.wrapped.GetAsync(request);
				}
			}

			private object optionsLock = new object();
			public Task<ISimpleHttpResponse> OptionsAsync(ISimpleHttpRequest request)
			{
				lock (optionsLock)
				{
					return this.wrapped.OptionsAsync(request);
				}
			}

			private object postLock = new object();
			public Task<ISimpleHttpResponse> PostAsync(ISimpleHttpRequest request)
			{
				lock (postLock)
				{
					return this.wrapped.PostAsync(request);
				}
			}

			private object putLock = new object();
			public Task<ISimpleHttpResponse> PutAsync(ISimpleHttpRequest request)
			{
				lock (putLock)
				{
					return this.wrapped.PutAsync(request);
				}
			}

			private object traceLock = new object();
			public Task<ISimpleHttpResponse> TraceAsync(ISimpleHttpRequest request)
			{
				lock (traceLock)
				{
					return this.wrapped.TraceAsync(request);
				}
			}
		}

		ISimpleHttpRequest GetRequest(string uri, string authorizationValue = null, string body = null)
		{
			var mockRequest = Mock.Create<ISimpleHttpRequest>();
			mockRequest.SetupGet(r => r.Uri).Returns(new Uri(uri));
			mockRequest.SetupGet(r => r.Body).Returns(body);
			mockRequest.SetupGet(r => r.Headers).Returns(new System.Net.WebHeaderCollection()
			{
				[System.Net.HttpRequestHeader.Authorization] = authorizationValue
			});

			return mockRequest.Object;
		}

		Mock<ISimpleHttpResponse> GetOkResponse(int pragmaValue, string body = null)
		{
			var mockResponse = Mock.Create<ISimpleHttpResponse>();
			mockResponse.SetupGet(r => r.Status).Returns(System.Net.HttpStatusCode.OK);
			mockResponse.SetupGet(r => r.Body).Returns(body);
			mockResponse.SetupGet(r => r.Headers).Returns(new System.Net.WebHeaderCollection()
			{
				[System.Net.HttpRequestHeader.Pragma] = pragmaValue.ToString()
			});

			return mockResponse;
		}

		[TestMethod]
		public void Signaller2_Constructor()
		{
			var instance = new Signaller2(Mock.Create<ISimpleHttpClient>().Object);

			Assert.AreEqual(null, instance.AuthenticationHeader);
			Assert.AreEqual(Signaller2.HeartbeatDisabled, instance.HeartbeatMs);
			Assert.AreEqual(Signaller2.DisconnectedId, instance.Id);
			Assert.AreEqual(false, instance.IsConnected);
		}

		[TestMethod]
		public void Signaller2_ConnectAsync_Succeeds()
		{
			// allocate our parameters
			var expectedPragmaId = 10;
			var expectedPeerName = "test_peer";
			var expectedBaseUri = "http://unit.test";

			// allocate our ingoing mocks (call validation)
			var expectedSignInResponse = GetOkResponse(expectedPragmaId);
			var mockHttp = Mock.Create<ISimpleHttpClient>();

			// allocate our outgoing mocks (data validation)
			var expectedFirstRequest = GetRequest($"{expectedBaseUri}/sign_in?peer_name={expectedPeerName}");
			var expectedParallelRequests = new ISimpleHttpRequest[] {
				GetRequest($"{expectedBaseUri}/heartbeat?peer_id={expectedPragmaId}"),
				GetRequest($"{expectedBaseUri}/wait?peer_id={expectedPragmaId}")
			};

			// compare two requests for equality
			Func<ISimpleHttpRequest, ISimpleHttpRequest, bool> comparer = (ISimpleHttpRequest req, ISimpleHttpRequest other) =>
			{
				bool match = req.Body == other.Body &&
					req.Uri == other.Uri &&
					req.Headers.Count == other.Headers.Count;

				if (match)
				{
					foreach (var key in req.Headers.AllKeys)
					{
						match = match && req.Headers[key] == other.Headers[key];
					}
				}

				return match;
			};

			// get async parameter matcher
			// this is non-trivial because 3 requests made be made, two of which occur on internal threads
			var matchCount = 0;
			Func<ISimpleHttpRequest, bool> isMatch = (ISimpleHttpRequest req) =>
			{
				bool res = false;

				if (matchCount == 0)
				{
					res = comparer(req, expectedFirstRequest);
				} else
				{
					res = expectedParallelRequests.Any(r => comparer(req, r));
				}

				matchCount++;

				return res;
			};

			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.Is(isMatch)))
				.Returns(Task.FromResult(expectedSignInResponse.Object));

			// allocator our instance
			var instance = new Signaller2(new ThreadsafeWrappedClient(mockHttp.Object));

			// bind a connected handler so we can ensure it is called
			var onConnectedCount = 0;
			instance.OnConnected += () => { onConnectedCount++; };

			// make the call, blocking until it completes
			var connectResult = instance.ConnectAsync(expectedBaseUri, expectedPeerName).Result;

			// ensure results are as expected
			Assert.AreEqual(true, connectResult);
			Assert.AreEqual(expectedPragmaId, instance.Id);
			Assert.AreEqual(true, instance.IsConnected);
			Assert.AreEqual(1, onConnectedCount);

			// could occur 1, 2, or 3 times
			// because /wait and /heartbeat are on threads
			// we ensure at least one, as the synchronous call to /sign_in must succeed
			mockHttp.Verify(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()), Occurred.AtLeast(1));
		}
		
		[TestMethod]
		public void Signaller2_ConnectAsync_Fails()
		{
			var mockHttp = Mock.Create<ISimpleHttpClient>();
			var instance = new Signaller2(new ThreadsafeWrappedClient(mockHttp.Object));

			// non-200 status code
			{
				var mockResponse = Mock.Create<ISimpleHttpResponse>();
				mockResponse.SetupGet(r => r.Status).Returns(System.Net.HttpStatusCode.BadRequest);

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Returns(Task.FromResult(mockResponse.Object));

				Exception onServerConnectFailureEx = null;
				instance.OnServerConnectionFailure += (Exception ex) => { onServerConnectFailureEx = ex; };

				var result = instance.ConnectAsync("http://unit.test", "test").Result;

				Assert.AreEqual("Invalid StatusCode 'BadRequest'", onServerConnectFailureEx.Message);
				Assert.AreEqual(false, result);
			}

			// http client unwrapped exception
			{
				var expectedException = new Exception("Forced failure");

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Throws(expectedException);

				Exception onServerConnectFailureEx = null;
				instance.OnServerConnectionFailure += (Exception ex) => { onServerConnectFailureEx = ex; };

				Exception thrownAtCallsite = null;
				try
				{
					var result = instance.ConnectAsync("http://unit.test", "test").Result;
				}
				catch (Exception ex)
				{
					thrownAtCallsite = ex;
				}
				finally
				{
					Assert.AreEqual(null, onServerConnectFailureEx);

					// we need to use InnerException because the hypermock will wrap
					// the exception for us but at runtime that will not occur
					Assert.AreEqual(expectedException, thrownAtCallsite.InnerException);
				}
			}

			// http client wrapped exception
			{
				var expectedException = new SimpleHttpClientException(new Exception("Forced failure"));

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Throws(expectedException);

				Exception onServerConnectFailureEx = null;
				instance.OnServerConnectionFailure += (Exception ex) => { onServerConnectFailureEx = ex; };

				Exception thrownAtCallsite = null;
				try
				{
					var result = instance.ConnectAsync("http://unit.test", "test").Result;
				}
				catch (Exception ex)
				{
					thrownAtCallsite = ex;
				}
				finally
				{
					Assert.AreEqual(expectedException, onServerConnectFailureEx);
					Assert.AreEqual(null, thrownAtCallsite);
				}
			}
		}

		[TestMethod]
		public void Signaller2_DisconnectAsync_Succeeds()
		{
			// allocate our parameters
			var expectedPragmaId = 10;
			var expectedBaseUri = "http://unit.test";

			// allocate our ingoing mocks (call validation)
			var expectedSignOutResponse = GetOkResponse(expectedPragmaId);
			var mockHttp = Mock.Create<ISimpleHttpClient>();

			// allocate our outgoing mocks (data validation)
			var expectedFirstRequest = GetRequest($"{expectedBaseUri}/sign_out?peer_id={expectedPragmaId}");

			// capture all the requests
			List<ISimpleHttpRequest> capturedRequests = new List<ISimpleHttpRequest>();
			Func<ISimpleHttpRequest, bool> captureParam = (ISimpleHttpRequest req) =>
			{
				capturedRequests.Add(req);

				return true;
			};

			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.Is(captureParam)))
				.Returns(Task.FromResult(expectedSignOutResponse.Object));

			// allocator our instance
			var instance = new Signaller2(new ThreadsafeWrappedClient(mockHttp.Object));

			// bind a disconnected handler so we can ensure it is called
			var onDisconnectedCount = 0;
			instance.OnDisconnected += () => { onDisconnectedCount++; };

			// make the call, blocking until it completes
			var connectResult = instance.ConnectAsync(expectedBaseUri, "test").Result;

			// ensure we've connected
			Assert.AreEqual(true, connectResult);
			Assert.AreEqual(true, instance.IsConnected);

			// make the call, blocking until it completes
			var disconnectResult = instance.DisconnectAsync().Result;

			// ensure results are as expected
			Assert.AreEqual(true, disconnectResult);
			Assert.AreEqual(Signaller2.DisconnectedId, instance.Id);
			Assert.AreEqual(false, instance.IsConnected);
			Assert.AreEqual(1, onDisconnectedCount);

			// we need to ensure we issued the /sign_out request
			Assert.IsTrue(capturedRequests.FirstOrDefault((req) =>
			{
				bool match = req.Body == expectedFirstRequest.Body &&
					req.Uri == expectedFirstRequest.Uri &&
					req.Headers.Count == expectedFirstRequest.Headers.Count;

				if (match)
				{
					foreach (var key in req.Headers.AllKeys)
					{
						match = match && req.Headers[key] == expectedFirstRequest.Headers[key];
					}
				}

				return match;
			}) != null, "missing /sign_out call");

			// could occur 2+ times
			// because /wait and /heartbeat are on threads
			// we ensure at least two, as the synchronous call to /sign_in must succeed
			// and the synchronous call to /sign_out must succeed
			mockHttp.Verify(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()), Occurred.AtLeast(2));
		}

		[TestMethod]
		public void Signaller2_DisconnectAsync_Fails()
		{
			var mockHttp = Mock.Create<ISimpleHttpClient>();
			var expectedSignOutResponse = GetOkResponse(10);

			var instance = new Signaller2(new ThreadsafeWrappedClient(mockHttp.Object));
			
			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
				.Returns(Task.FromResult(expectedSignOutResponse.Object));

			// issue a connect response so we can accurately test the disconnect failures
			var connectResult = instance.ConnectAsync("http://unit.test", "test").Result;

			// ensure we've connected
			Assert.AreEqual(true, connectResult);
			Assert.AreEqual(true, instance.IsConnected);

			// non-200 status code
			{
				var mockResponse = Mock.Create<ISimpleHttpResponse>();
				mockResponse.SetupGet(r => r.Status).Returns(System.Net.HttpStatusCode.BadRequest);

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Returns(Task.FromResult(mockResponse.Object));
				
				var result = instance.DisconnectAsync().Result;
				Assert.AreEqual(false, result);
			}

			// http client unwrapped exception
			{
				var expectedException = new Exception("Forced failure");

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Throws(expectedException);
				
				Exception thrownAtCallsite = null;
				try
				{
					var result = instance.DisconnectAsync().Result;
				}
				catch (Exception ex)
				{
					thrownAtCallsite = ex;
				}
				finally
				{
					// we need to use InnerException because the hypermock will wrap
					// the exception for us but at runtime that will not occur
					Assert.AreEqual(expectedException, thrownAtCallsite.InnerException);
				}
			}

			// http client wrapped exception
			{
				var expectedException = new SimpleHttpClientException(new Exception("Forced failure"));

				mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
					.Throws(expectedException);

				Exception thrownAtCallsite = null;
				try
				{
					var result = instance.DisconnectAsync().Result;
				}
				catch (Exception ex)
				{
					thrownAtCallsite = ex;
				}
				finally
				{
					// we need to use InnerException because the hypermock will wrap
					// the exception for us but at runtime that will not occur
					Assert.AreEqual(expectedException, thrownAtCallsite.InnerException);
				}
			}
		}

	}
}
