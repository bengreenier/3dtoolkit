#pragma once

#include <functional>
#include <string>
#include <fstream>
#include <thread>

#include "webrtc/base/thread.h"
#include "webrtc/base/sigslot.h"
#include "third_party/jsoncpp/source/include/json/json.h"

// TODO(bengreenier): support other platforms
#ifdef WEBRTC_WIN
#include "webrtc/base/win32socketserver.h"
#include "webrtc/base/win32socketinit.h"
#endif

#include "server_authentication_provider.h"
#include "turn_credential_provider.h"
#include "main_window.h"
#include "conductor.h"

/// <summary>
/// Used to initialize a server with authentication, dynamic turn creds, etc
/// </summary>
/// <remarks>
/// Unless you're doing your own thread management you likely want to consume
/// <see cref="InitializerWrapper"/> instead
/// </remarks>
class Initializer : public sigslot::has_slots<>
{
public:
	struct InitializedValues
	{
		std::string serverAddress;
		int serverPort;
		int heartbeatIntervalMs;
		std::string accessToken;
		std::string turnUsername;
		std::string turnPassword;
	};

	Initializer(const std::string& configPath, std::function<void(InitializedValues)> onRun);
	~Initializer();

	void WaitForCompletion();

	// implement runnable
	void Run();

	void OnAuthenticationComplete(const AuthenticationProviderResult& creds);
	void OnCredentialsRetrieved(const TurnCredentials& creds);

private:
	void OnInitialized();
	void OnError(const std::string& errorDesc);

	// values ordered intentionally to indicate the order of the full FSM flow
	enum State
	{
		NONE = 0,
		AUTHENTICATING,
		GETTING_TURN_CREDS,
		INITIALIZED
	};
	State state_;
	rtc::Event completion_event_;
	std::string config_path_;
	
	// generated values
	std::string access_token_;
	std::string turn_username_;
	std::string turn_password_;
	std::string server_address_;
	int server_port_;
	int heartbeat_interval_;

	// run args
	std::function<void(InitializedValues)> on_run_;

	std::unique_ptr<ServerAuthenticationProvider> auth_provider_;
	std::unique_ptr<TurnCredentialProvider> turn_provider_;
};

/// <summary>
/// A lightweight wrapper for <see cref="Initializer"/> that handles
/// threading and blocks on initialization until it is complete
/// </summary>
class InitializerWrapper
{
public:
	InitializerWrapper(const std::string& configPath,
		std::function<void(Initializer::InitializedValues)> onRun);
		~InitializerWrapper();
	
	void Run();

private:
	std::unique_ptr<std::thread> thread_;
	std::unique_ptr<Initializer> instance_;
	std::string config_path_;
	std::function<void(Initializer::InitializedValues)> on_run_;
};