#pragma once

#include <map>
#include <string>
#include <functional>
#include "ssl_capable_socket.h"
#include "webrtc/base/physicalsocketserver.h"
#include "webrtc/base/sigslot.h"

class RtcHttp : public sigslot::has_slots<>
{
public:
	enum RtcHttpMethod
	{
		GET,
		POST,
		PUT,
		DEL,
		OPTIONS,
		HEAD,
		TRACE,
		CONNECT
	};

	enum RtcHttpFailureCode
	{
		Success = 0,
		GenericFailure,
		NameResolutionFailure,
		ConnectionFailure,
		HttpSendFailure,
		HttpReceiveFailure,
		HttpParseFailure
	};
	
	struct RtcHttpResult
	{
		RtcHttpFailureCode code;
		int status;
		std::map<std::string, std::string> headers;
		std::string body;

		RtcHttpResult() : code(RtcHttpFailureCode::GenericFailure) {}
	};
	
	sigslot::signal1<const RtcHttpResult&> SignalComplete;

	class RtcHttpResultHandler : public sigslot::has_slots<>
	{
	public:
		RtcHttpResultHandler(RtcHttp& req, const std::function<void(const RtcHttpResult&)>& cb) : m_cb(cb)
		{
			req.SignalComplete.connect<RtcHttpResultHandler>(this, &RtcHttpResultHandler::OnComplete);
		}

		void OnComplete(const RtcHttpResult& res)
		{
			m_cb(res);
		}
		
	private:
		const std::function<void(const RtcHttpResult&)>& m_cb;
	};

	RtcHttp(const std::string& uri,
		std::string body = nullptr);
	
	RtcHttp(RtcHttpMethod method,
		const std::string& uri,
		std::string body = nullptr);

	RtcHttp(RtcHttpMethod method,
		const std::string& uri,
		std::map<std::string, std::string> headers,
		std::string body = nullptr);

private:
	void OnResolveResult(rtc::AsyncResolverInterface* resolver);
	void OnConnect(rtc::AsyncSocket* socket);
	void OnRead(rtc::AsyncSocket* socket);
	void OnClose(rtc::AsyncSocket* socket, int err);
	
	std::string MethodToString(RtcHttpMethod method);

	std::string PrepareRequest(const std::string& method,
		const std::string& fragment,
		std::map<std::string, std::string> headers);

	RtcHttpResult m_finalResult;
	RtcHttpMethod m_method;
	std::string m_uri;
	rtc::SocketAddress m_address;
	std::string m_pathAndQuery;
	std::map<std::string, std::string> m_headers;
	std::string m_body;
	rtc::Thread* m_unownedProcessingThread;
	std::unique_ptr<rtc::AsyncResolver> m_resolver;
	std::unique_ptr<SslCapableSocket> m_socket;
};