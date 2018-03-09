#include "rtc_http.h"

// used for helpers, see below
#include <sstream>
#include <vector>
#include <iterator>

namespace
{
	// these helpers are useful for parsing the http response
	
	template<typename Out>
	void split(const std::string &s, char delim, Out result) {
		std::stringstream ss(s);
		std::string item;
		while (std::getline(ss, item, delim)) {
			*(result++) = item;
		}
	}

	std::vector<std::string> split(const std::string &s, char delim) {
		std::vector<std::string> elems;
		split(s, delim, std::back_inserter(elems));
		return elems;
	}
}

RtcHttp::RtcHttp(const std::string& uri,
	std::string body) : RtcHttp(RtcHttpMethod::GET, uri, body)
{
}

RtcHttp::RtcHttp(RtcHttpMethod method,
	const std::string& uri,
	std::string body) : RtcHttp(method, uri, std::map<std::string, std::string>(), body)
{
}

RtcHttp::RtcHttp(RtcHttpMethod method,
	const std::string& uri,
	std::map<std::string, std::string> headers,
	std::string body) : 
	m_method(method),
	m_headers(headers),
	m_body(body)
{
	// use the current thread or wrap a thread for processing
	auto thread = rtc::Thread::Current();

	m_unownedProcessingThread = thread == nullptr ?
		rtc::ThreadManager::Instance()->WrapCurrentThread() : thread;

	// parse the uri
	std::string serverAddress = uri;
	bool shouldUseSsl = false;

	if (serverAddress.substr(0, 8).compare("https://") == 0)
	{
		shouldUseSsl = true;
		serverAddress = serverAddress.substr(8);
	}
	else if (serverAddress.substr(0, 7).compare("http://") == 0)
	{
		serverAddress = serverAddress.substr(7);
	}

	auto pathStart = serverAddress.find_first_of('/');

	if (pathStart != std::string::npos)
	{
		m_pathAndQuery = serverAddress.substr(pathStart);
		serverAddress = serverAddress.substr(0, pathStart);
		m_uri = serverAddress;
	}
	else
	{
		// TODO(bengreenier): this ssl thing is roundabout and slow - improve
		m_uri = (shouldUseSsl ? "https" : "http") + std::string("://") + serverAddress;
		m_pathAndQuery = "/";
	}

	m_address.SetIP(serverAddress);

	auto portStart = serverAddress.find_first_of(':');
	if (portStart != std::string::npos)
	{
		m_address.SetPort(atoi(serverAddress.substr(portStart + 1).c_str()));
	}

	// once we've parsed, we can resolve if needed
	if (m_address.IsUnresolvedIP())
	{
		m_resolver.reset(new rtc::AsyncResolver());
		m_resolver->SignalDone.connect(this, &RtcHttp::OnResolveResult);
		m_resolver->Start(m_address);
	}
	// otherwise we can just keep moving along
	else
	{
		OnResolveResult(nullptr);
	}
}

void RtcHttp::OnResolveResult(rtc::AsyncResolverInterface* resolver)
{
	// if we have a resolver, we actually resolved something
	// we'll process that before proceeding
	if (resolver != nullptr)
	{
		// we failed
		if (resolver->GetError() != 0)
		{
			resolver->Destroy(false);

			// explain why we failed
			RtcHttpResult res;
			res.code = RtcHttpFailureCode::NameResolutionFailure;

			// fail
			return SignalComplete.emit(res);
		}
		else
		{
			// store the resolved address, overwritting the unresolved address
			m_address = resolver->address();
		}
	}

	// now we can start to issue the request
	//
	// TODO(bengreenier): this ssl approach is slow - improve
	m_socket.reset(new SslCapableSocket(m_address.family(),
		m_uri.substr(0, 8).compare("https://") == 0,
		m_unownedProcessingThread));

	// try to connect
	if (SOCKET_ERROR == m_socket->Connect(m_address))
	{
		// explain why we failed
		RtcHttpResult res;
		res.code = RtcHttpFailureCode::ConnectionFailure;

		// fail
		return SignalComplete.emit(res);
	}
}

void RtcHttp::OnConnect(rtc::AsyncSocket* socket)
{
	// build the request
	auto req = PrepareRequest(MethodToString(m_method), m_pathAndQuery, m_headers);

	// append the body if needed
	if (m_body.length() > 0)
	{
		req += m_body + "\r\n";
	}

	auto sentBytes = socket->Send(req.c_str(), req.length());

	// see if we sent all the data, or if we failed
	if (sentBytes != req.length())
	{
		// explain why we failed
		RtcHttpResult res;
		res.code = RtcHttpFailureCode::HttpSendFailure;
		SignalComplete.emit(res);
	}
}

void RtcHttp::OnRead(rtc::AsyncSocket* socket)
{
	// read all data into data
	std::string data;
	char buffer[0xffff];
	do
	{
		int bytes = socket->Recv(buffer, sizeof(buffer), nullptr);
		if (bytes <= 0)
		{
			break;
		}

		data.append(buffer, bytes);
	} while (true);

	// create the result object
	RtcHttpResult res;

	// if we have no data, fail out
	if (data.length() == 0)
	{
		res.code = RtcHttpFailureCode::HttpReceiveFailure;
		return SignalComplete.emit(res);
	}

	// parse the status line
	//
	// this should look like: HTTP/1.1 200 OK\r\n
	//
	auto statusLine = data.substr(data.find_first_of("\r\n"));
	auto statusParts = split(data, ' ');
	
	// grab the status, parse it
	res.status = atoi(statusParts.at(1).c_str());

	// parse the headers
	//
	// these should look like: HeaderName: HeaderValue\r\n
	// repeated n times, followed by \r\n
	//
	size_t headerStart = data.find("\r\n\r\n");
	size_t headerEnd = data.find("\r\n\r\n", headerStart);
	if (headerStart != std::string::npos)
	{
		auto headerPos = headerStart;
		while (headerPos < headerEnd)
		{
			auto headerLine = data.substr(headerPos, data.find_first_of('\r\n', headerPos) - headerPos);
			auto headerLineParts = split(headerLine, ' ');

			// insert the header
			res.headers[headerLineParts.at(0)] = headerLineParts.at(1);
		}
	}

	// from the marker (including it's contents) to the end is now body
	res.body = data.substr(headerEnd + 4);

	// at this point, we're good
	res.code = RtcHttpFailureCode::Success;

	// we store the final result and close out - we'll share it with the user in OnClose
	m_finalResult = res;

	// close the socket
	socket->Close();
}

void RtcHttp::OnClose(rtc::AsyncSocket* socket, int err)
{
	// we're done! complete in whatever state we're in
	SignalComplete(m_finalResult);
}

std::string RtcHttp::MethodToString(RtcHttpMethod method)
{
	switch (method)
	{
	case RtcHttpMethod::CONNECT:
		return "CONNECT";
	case RtcHttpMethod::DEL:
		return "DELETE";
	case RtcHttpMethod::GET:
		return "GET";
	case RtcHttpMethod::HEAD:
		return "HEAD";
	case RtcHttpMethod::OPTIONS:
		return "OPTIONS";
	case RtcHttpMethod::POST:
		return "POST";
	case RtcHttpMethod::PUT:
		return "PUT";
	case RtcHttpMethod::TRACE:
		return "TRACE";
	default:
		throw std::exception("Unable to parse RtcHttpMethod");
	}
}

std::string RtcHttp::PrepareRequest(const std::string& method, const std::string& fragment, std::map<std::string, std::string> headers)
{
	std::string result;

	for (size_t i = 0; i < method.length(); ++i)
	{
		result += (char)toupper(method[i]);
	}

	result += " " + fragment + " HTTP/1.1\r\n";

	for (auto it = headers.begin(); it != headers.end(); ++it)
	{
		result += it->first + ": " + it->second + "\r\n";
	}

	result += "\r\n";

	return result;
}