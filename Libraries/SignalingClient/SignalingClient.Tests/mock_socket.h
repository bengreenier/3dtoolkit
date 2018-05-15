#pragma once

#include "webrtc/base/asyncsocket.h"
#include "webrtc/base/sigslot.h"

using namespace sigslot;
using namespace rtc;

/// <summary>
/// Mock Socket implementation, exposing data and signals to allow instrumentation
/// </summary>
/// <remarks>
/// Uses mocking pattern of Data, Method, Signal for organization
/// </remarks>
class MockSocket : public AsyncSocket, public sigslot::has_slots<>
{
public:
	void SetUseSsl(const bool& useSsl) { Signal_SetUseSsl.emit(useSsl); }

	signal1<const bool&> Signal_SetUseSsl;

	bool Data_GetUseSsl;

	bool GetUseSsl() const { return Data_GetUseSsl; }

	SocketAddress Data_GetLocalAddress;

	SocketAddress GetLocalAddress() const { return Data_GetLocalAddress; }

	SocketAddress Data_GetRemoteAddress;

	SocketAddress GetRemoteAddress() const { return Data_GetRemoteAddress; }

	int Data_Bind;

	int Bind(const SocketAddress& addr) { Signal_Bind.emit(addr); return Data_Bind; }

	signal1<const SocketAddress&> Signal_Bind;

	int Data_Connect;

	int Connect(const SocketAddress& addr) { Signal_Connect.emit(addr); return Data_Connect; }

	signal1<const SocketAddress&> Signal_Connect;

	int Data_Send;

	int Send(const void* pv, size_t cb) { Signal_Send.emit(pv, cb); return Data_Send; }

	signal2<const void*, size_t> Signal_Send;

	int Data_SendTo;

	int SendTo(const void* pv, size_t cb, const SocketAddress& addr) { Signal_SendTo.emit(pv, cb, addr); return Data_SendTo; }

	signal3<const void*, size_t, const SocketAddress&> Signal_SendTo;

	int Data_Recv;

	int Recv(void* pv, size_t cb, int64_t* timestamp) { Signal_Recv.emit(pv, cb, timestamp); return Data_Recv; }

	signal3<const void*, size_t, int64_t*> Signal_Recv;

	int Data_RecvFrom;

	int RecvFrom(void* pv,
		size_t cb,
		SocketAddress* paddr,
		int64_t* timestamp) {
		Signal_RecvFrom.emit(pv, cb, paddr, timestamp); return Data_RecvFrom;
	}

	signal4<const void*, size_t, SocketAddress*, int64_t*> Signal_RecvFrom;

	int Data_Listen;

	int Listen(int backlog) { Signal_Listen.emit(backlog); return Data_Listen; }

	signal1<int> Signal_Listen;

	AsyncSocket* Data_Accept;

	AsyncSocket* Accept(SocketAddress* paddr) { Signal_Accept.emit(paddr); return Data_Accept; }

	signal1<SocketAddress*> Signal_Accept;

	int Data_Close;

	int Close() { Signal_Close.emit(); return Data_Close; }

	signal0<> Signal_Close;

	int Data_GetError;

	int GetError() const { return Data_GetError; };

	void SetError(int error) { Signal_SetError.emit(error); }
	
	signal1<int> Signal_SetError;
	
	Socket::ConnState Data_GetState;

	Socket::ConnState GetState() const { return Data_GetState; }

	int Data_EstimateMTU;

	int EstimateMTU(uint16_t* mtu) { Signal_EstimateMTU.emit(mtu); return Data_EstimateMTU; }

	signal1<uint16_t*> Signal_EstimateMTU;

	int Data_GetOption;

	int GetOption(AsyncSocket::Option opt, int* value) { Signal_GetOption.emit(opt, value); return Data_GetOption; }

	signal2<AsyncSocket::Option, int*> Signal_GetOption;

	int Data_SetOption;

	int SetOption(AsyncSocket::Option opt, int value) { Signal_SetOption.emit(opt, value); return Data_SetOption; }

	signal2<AsyncSocket::Option, int> Signal_SetOption;

};

