
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebRtcWrapper.Signalling;
using WebRtcWrapper.UnitTests.Mocks;

namespace WebRtcWrapper.UnitTests
{
    [TestClass]
    public class Signalling2Tests
    {
		MockSimpleHttpClient mockHttp;
		Signaller2 instance;

		[TestInitialize]
		public void Init()
		{
			this.mockHttp = new MockSimpleHttpClient();
			this.instance = new Signaller2(this.mockHttp);
		}

        [TestMethod]
        public void TestMethod1()
        {
			this.mockHttp
        }
    }
}
