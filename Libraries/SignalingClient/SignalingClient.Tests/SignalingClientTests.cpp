#include <gtest\gtest.h>
#include <gmock\gmock.h>

#include "peer_connection_client.h"

#include "RtcEventLoop.h"
#include "Observers\NoFailureObserver.hpp"

#pragma comment(lib, "webrtc.lib")
#pragma comment(lib, "Winmm.lib")

using namespace testing;
using namespace std;

class MockSslCapableSocketFactory : public SslCapableSocket::Factory
{
public:
	MOCK_METHOD3(Allocate, unique_ptr<SslCapableSocket>(const int&, const bool&, weak_ptr<Thread>));
};

class FakeSslCapableSocket : public SslCapableSocket, public rtc::MessageHandler
{
public:
	FakeSslCapableSocket(const int& family, const bool& useSsl, std::weak_ptr<Thread> signalingThread) :
		SslCapableSocket(family, useSsl, signalingThread),
		data_pos_(0), state_(Socket::ConnState::CS_CLOSED) {}

	int Connect(const SocketAddress& addr) override
	{
		state_ = Socket::ConnState::CS_CONNECTING;
		
		// simulate this taking some time
		if (auto marshalledThread = signaling_thread_.lock())
		{
			marshalledThread->PostDelayed(RTC_FROM_HERE, 1000, this);
		}

		return 0;
	}

	int Send(const void* pv, size_t cb) override
	{
		// simulate this taking some time
		if (auto marshalledThread = signaling_thread_.lock())
		{
			marshalledThread->PostDelayed(RTC_FROM_HERE, 1000, this);
		}

		return (int)cb;
	}

	int Recv(void* pv, size_t cb, int64_t* timestamp) override
	{
		// TODO(bengreenier): this is fucked and is the blocker ATM 6/16
		if (data_pos_ < data_str_.length())
		{
		data_str_.copy((char*)pv, cb, data_pos_);
		data_pos_ = cb;
		return (int)cb;
	}

	int Close() override
	{
		state_ = Socket::ConnState::CS_CLOSED;
		
		// simulate this taking some time
		if (auto marshalledThread = signaling_thread_.lock())
		{
			marshalledThread->PostDelayed(RTC_FROM_HERE, 1000, this);
		}

		return 0;
	}

	Socket::ConnState GetState() const override
	{
		return state_;
	}

	void OnMessage(rtc::Message* msg) override
	{
		if (state_ == Socket::ConnState::CS_CONNECTING)
		{
			state_ = Socket::ConnState::CS_CONNECTED;
			SignalConnectEvent.emit(this);
		}
		else if (state_ == Socket::ConnState::CS_CLOSED)
		{
			RefireCloseEvent(this, 0);
		}
		else
		{
			RefireReadEvent(this);
		}
	}
protected:
	const string data_str_ = "HTTP/1.1 200 OK\r\nX-Powered-By: Express\r\nPragma: 2\r\nContent-Type: text/plain;charset=utf-8\r\nContent-Length: 18\r\nETag: W/\"12-mDfvg2OymdSB0T1Fwl+XHiLarp8\"\r\nConnection: keep-alive\r\n\r\ntest, 2, 1\r\ntest, 1, 0\r\n\0";
	int data_pos_;

	Socket::ConnState state_;
};

class MockSslCapableSocket : public SslCapableSocket
{
public:
	MockSslCapableSocket(const int& family, const bool& useSsl, std::weak_ptr<Thread> signalingThread) :
		SslCapableSocket(family, useSsl, signalingThread),
		fake_(family, useSsl, signalingThread) {}

	MOCK_METHOD1(SetUseSsl, void(const bool& useSsl));
	MOCK_CONST_METHOD0(GetUseSsl, bool());

	MOCK_CONST_METHOD0(GetLocalAddress, SocketAddress());
	MOCK_CONST_METHOD0(GetRemoteAddress, SocketAddress());
	MOCK_METHOD1(Bind, int(const SocketAddress& addr));
	MOCK_METHOD1(Connect, int(const SocketAddress& addr));
	MOCK_METHOD2(Send, int(const void* pv, size_t cb));
	MOCK_METHOD3(SendTo, int(const void* pv, size_t cb, const SocketAddress& addr));
	MOCK_METHOD3(Recv, int(void* pv, size_t cb, int64_t* timestamp));
	MOCK_METHOD4(RecvFrom, int(void* pv,
		size_t cb,
		SocketAddress* paddr,
		int64_t* timestamp));
	MOCK_METHOD1(Listen, int(int backlog));
	MOCK_METHOD1(Accept, AsyncSocket*(SocketAddress* paddr));
	MOCK_METHOD0(Close, int());
	MOCK_CONST_METHOD0(GetError, int());
	MOCK_METHOD1(SetError, void(int error));
	MOCK_CONST_METHOD0(GetState, Socket::ConnState());
	MOCK_METHOD2(GetOption, int(AsyncSocket::Option opt, int* value));
	MOCK_METHOD2(SetOption, int(AsyncSocket::Option opt, int value));

	// Delegates the default actions of the methods to a FakeFoo object.
		// This must be called *before* the custom ON_CALL() statements.
	void DelegateToFake()
	{
		ON_CALL(*this, Connect(_))
			.WillByDefault(Invoke(&fake_, &FakeSslCapableSocket::Connect));
		ON_CALL(*this, Send(_, _))
			.WillByDefault(Invoke(&fake_, &FakeSslCapableSocket::Send));
		ON_CALL(*this, Recv(_, _, _))
			.WillByDefault(Invoke(&fake_, &FakeSslCapableSocket::Recv));
		ON_CALL(*this, Close())
			.WillByDefault(Invoke(&fake_, &FakeSslCapableSocket::Close));
		ON_CALL(*this, GetState())
			.WillByDefault(Invoke(&fake_, &FakeSslCapableSocket::GetState));

		// map in the fake event emitters
		MapUnderlyingEvents(&fake_);
	}
private:
	FakeSslCapableSocket fake_;
};

TEST(SignalingClientTests, SignalConnect)
{
	shared_ptr<MockSslCapableSocketFactory> factory = make_shared<MockSslCapableSocketFactory>();
	NoFailureObserver obs;

	// make the mock allocator allocate mock sockets
	ON_CALL(*factory, Allocate(_, _, _))
		.WillByDefault(Invoke([](const int& a, const bool& b , weak_ptr<Thread> c)
	{
		auto mockSocket = make_unique<MockSslCapableSocket>(a, b, c);

		// make the mock socket use the fake for some critical behaviors
		mockSocket->DelegateToFake();

		// use this mocked instance
		return mockSocket;
	}));

	// scope for loop guard
	{
		// tie client lifetime to loop guard
		shared_ptr<PeerConnectionClient> client;
		RtcEventLoop loop([&]()
		{
			// alloc client, bind observer
			client = make_shared<PeerConnectionClient>(factory);
			client->RegisterObserver(&obs);

			// attempt valid connect
			client->Connect("localhost", 1, "test");
		});

		// block test thread waiting for observer
		// expect the observer to be truthy, indicating there are no ServerConnectionFailures
		EXPECT_TRUE(obs.Wait());

		// rely on RAII to kill the loop
	}
}

TEST(SignalingClientTests, AllocateInvoked)
{
	shared_ptr<MockSslCapableSocketFactory> factory = make_shared<MockSslCapableSocketFactory>();
	NoFailureObserver obs;

	// expect 4 socket allocations that do a normal allocation under the hood
	EXPECT_CALL(*factory, Allocate(_, _, _))
		.Times(Exactly(4))
		.WillRepeatedly(Invoke([](const int& a, const bool& b, weak_ptr<Thread> c) { return make_unique<SslCapableSocket>(a, b, c); }));

	// scope for loop guard
	{
		// tie client lifetime to loop guard
		shared_ptr<PeerConnectionClient> client;
		RtcEventLoop loop([&]()
		{
			// alloc client, bind observer
			client = make_shared<PeerConnectionClient>(factory);
			client->RegisterObserver(&obs);

			// attempt valid connect
			client->Connect("localhost", 1, "test");
		});

		// block test thread waiting for observer
		// expect the observer to be truthy, indicating there are no ServerConnectionFailures
		EXPECT_TRUE(obs.Wait());

		// rely on RAII to kill the loop
	}
}
