#include "stdafx.h"
#include "CppUnitTest.h"
#include "http_request.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace SignalingClientUnitTests
{		
	TEST_CLASS(HttpRequestTests), public HttpRequestObserver
	{
	public:
		
		TEST_METHOD(ParsingTest)
		{

			HttpRequest req("GET", "https://bing.com");
			rtc::NullSocketServer fakeSocket;
			fakeSocket.CreateSocket(1)->

			Current = CurrentTest::ParsingTest;
			req.RegisterObserver(this);
		}

		void OnRequestComplete(HttpRequest* req, const HttpResponse& res)
		{
			if (Current == CurrentTest::ParsingTest)
			{
				Assert::AreEqual(res.)
			}
		}

	private:
		enum CurrentTest
		{
			None,
			ParsingTest
		};
		static CurrentTest Current;
	};
}