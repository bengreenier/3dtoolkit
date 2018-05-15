#include "stdafx.h"
#include "CppUnitTest.h"
#include "peer_connection_client.h"

#include "mock_socket.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace SignalingClientTests
{
	class MockSocketFactory : public SslCapableSocket::Factory
	{
	public:
		std::shared_ptr<AsyncSocket> Allocate(const int& port, const bool& useSsl, Thread* signal)
		{
			return std::make_shared<MockSocket>(port, useSsl, signal);
		}
	};

	TEST_CLASS(PeerConnectionClientTests)
	{
	public:

		TEST_METHOD(ShouldConnect_Success)
		{
			std::string expectedClientName = "clientName";
			std::string expectedServer = "test.com";
			int expectedPort = 1010;

			MockSocketFactory factory;
			PeerConnectionClient client(factory);

			client.Connect(expectedServer, expectedPort, expectedClientName);
		}

	};
}