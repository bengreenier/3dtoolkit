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

class InitializerWrapper
{
public:
	InitializerWrapper(const std::string& configPath,
		std::function<void(Initializer::InitializedValues)> onRun) : config_path_(configPath), on_run_(onRun) {}
	~InitializerWrapper() { if (thread_.get() != nullptr) thread_->join(); }
	
	void Run()
	{
		rtc::Thread* procThread = nullptr;
		rtc::Event threadRunning(false, false);
		thread_.reset(new std::thread([&] {
			// TODO(bengreenier): support other platforms
			rtc::Win32Thread w32_thread;
			rtc::ThreadManager::Instance()->SetCurrentThread(&w32_thread);

			procThread = &w32_thread;

			instance_.reset(new Initializer(config_path_, on_run_));
			instance_->Run();

			threadRunning.Set();

			w32_thread.ProcessMessages(w32_thread.kForever);
		}));
		threadRunning.Wait(rtc::Event::kForever);
		instance_->WaitForCompletion();
		procThread->Stop();
	}

private:
	std::unique_ptr<std::thread> thread_;
	std::unique_ptr<Initializer> instance_;
	std::string config_path_;
	std::function<void(Initializer::InitializedValues)> on_run_;
};