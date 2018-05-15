#pragma once

#include "webrtc/base/physicalsocketserver.h"

class HttpServer
{
public:
	HttpServer();
	~HttpServer();

	const int port() { return m_port; }

private:
	int m_port;
};

