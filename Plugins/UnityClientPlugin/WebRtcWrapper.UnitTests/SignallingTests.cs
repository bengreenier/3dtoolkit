using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HyperMock;
using WebRtcWrapper.Signalling;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace WebRtcWrapper.UnitTests
{
    [TestClass]
    public class SignallingTests
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

		private Mock<ISimpleHttpClient> mockHttp;
		private Signaller instance;

		[TestInitialize]
		public void Init()
		{
			this.mockHttp = Mock.Create<ISimpleHttpClient>();
			this.instance = new Signaller(new ThreadsafeWrappedClient(mockHttp.Object));
		}

		[TestCleanup]
		public void Cleanup()
		{
			this.instance.Dispose();
		}

		[TestMethod]
		public void Signaller2_Constructor()
		{
			Assert.AreEqual(null, instance.AuthenticationHeader);
			Assert.AreEqual(Signaller.HeartbeatDisabled, instance.HeartbeatMs);
			Assert.AreEqual(Signaller.DisconnectedId, instance.Id);
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
			Assert.AreEqual(Signaller.DisconnectedId, instance.Id);
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
			var expectedSignOutResponse = GetOkResponse(10);
			
			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()))
				.Returns(Task.FromResult(expectedSignOutResponse.Object));

			// force things to look connected
			typeof(Signaller).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(p => p.Name == "connectedUri").SetValue(instance, new Uri("http://unit.test"));
			typeof(Signaller).GetProperties(BindingFlags.Instance | BindingFlags.Public).First(p => p.Name == "Id").SetValue(instance, 10);
			typeof(Signaller).GetProperties(BindingFlags.Instance | BindingFlags.Public).First(p => p.Name == "IsConnected").SetValue(instance, true);

			// ensure we've connected
			Assert.AreEqual(10, instance.Id);
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

		[TestMethod]
		public void Signaller2_Heartbeat_Succeeds()
		{
			var expectedUri = new Uri("http://unit.test");

			var mockResponse = GetOkResponse(10);

			// capture all the requests
			List<ISimpleHttpRequest> capturedRequests = new List<ISimpleHttpRequest>();
			Func<ISimpleHttpRequest, bool> captureParam = (ISimpleHttpRequest req) =>
			{
				if (req.Uri.PathAndQuery == "/heartbeat?peer_id=-1")
				{
					capturedRequests.Add(req);
				}

				return true;
			};

			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.Is(captureParam)))
				.Returns(Task.FromResult(mockResponse.Object));

			// we'll use a 0.5s beat for our test
			instance.HeartbeatMs = 500;

			// do some reflection to set the connectUri (a dependency of the heartbeat task)
			typeof(Signaller).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(p => p.Name == "connectedUri").SetValue(instance, expectedUri);

			// start the background tasks
			typeof(Signaller).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).First(m => m.Name == "StartBackgroundHttp").Invoke(instance, null);

			// block execution for 1.5s, should be enough for 2 iterations heartbeat
			Task.Delay(1500).Wait();

			instance.Dispose();
			
			// 2 occcurances for /heartbeat and 2 occurances for /wait
			mockHttp.Verify(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()), Occurred.AtLeast(4));
			
			// we need to ensure we issued the /heartbeat request at least twice
			Assert.IsTrue(capturedRequests.Count((req) =>
			{
				// peer_id should be -1 because we're reflecting, not using a real valid instance
				bool match = req.Body == null &&
					req.Uri == new Uri(expectedUri, "/heartbeat?peer_id=-1") &&
					req.Headers.Count == 1 &&
					req.Headers[System.Net.HttpRequestHeader.Authorization] == "";

				return match;
			}) >= 2, "missing /heartbeat call");
		}

		[TestMethod]
		public void Signaller2_Wait_Succeeds()
		{
			var expectedUri = new Uri("http://unit.test");

			var mockResponse = GetOkResponse(10);

			// capture all the requests
			List<ISimpleHttpRequest> capturedRequests = new List<ISimpleHttpRequest>();
			Func<ISimpleHttpRequest, bool> captureParam = (ISimpleHttpRequest req) =>
			{
				if (req.Uri.PathAndQuery == "/wait?peer_id=-1")
				{
					capturedRequests.Add(req);
				}

				return true;
			};

			// configure the mock for the call
			mockHttp.Setup(c => c.GetAsync(Param.Is(captureParam)))
				.Returns(Task.Delay(100).ContinueWith(t => mockResponse.Object));

			// no heartbeat for this test
			instance.HeartbeatMs = Signaller.HeartbeatDisabled;

			// do some reflection to set the connectUri (a dependency of the heartbeat task)
			typeof(Signaller).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(p => p.Name == "connectedUri").SetValue(instance, expectedUri);

			// start the background tasks
			typeof(Signaller).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).First(m => m.Name == "StartBackgroundHttp").Invoke(instance, null);

			// block execution for 1s, should be enough for 2 iterations wait
			Task.Delay(1000).Wait();

			instance.Dispose();

			// 2 occurances for /wait
			mockHttp.Verify(c => c.GetAsync(Param.IsAny<ISimpleHttpRequest>()), Occurred.AtLeast(2));

			// we need to ensure we issued the /heartbeat request at least twice
			Assert.IsTrue(capturedRequests.Count((req) =>
			{
				// peer_id should be -1 because we're reflecting, not using a real valid instance
				bool match = req.Body == null &&
					req.Uri == new Uri(expectedUri, "/wait?peer_id=-1") &&
					req.Headers.Count == 1 &&
					req.Headers[System.Net.HttpRequestHeader.Authorization] == "";

				return match;
			}) >= 2, "missing /wait call");
		}

	}
}
