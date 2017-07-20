#include "pch.h"
#include "initializer.h"

Initializer::Initializer(const std::string& configPath, std::function<void(InitializedValues)> onRun) :
	state_(State::NONE),
	completion_event_(false, false),
	config_path_(configPath),
	on_run_(onRun),
	server_address_(""),
	server_port_(-1)
{
	// TODO(bengreenier): support other platforms
#ifdef WEBRTC_WIN
	rtc::EnsureWinsockInit();
#endif

	rtc::InitializeSSL();
}

Initializer::~Initializer()
{
	rtc::CleanupSSL();
}

void Initializer::WaitForCompletion()
{
	completion_event_.Wait(rtc::Event::kForever);
}

void Initializer::Run()
{
	// parse config
	std::ifstream configFile(config_path_);
	if (configFile.good())
	{
		Json::Reader reader;
		Json::Value root = NULL;
		reader.parse(configFile, root, true);
		if (root.isMember("server"))
		{
			server_address_ = root.get("server", "").asString();
		}

		if (root.isMember("port"))
		{
			server_port_ = root.get("port", -1).asInt();
		}

		if (root.isMember("heartbeat"))
		{
			heartbeat_interval_ = root.get("heartbeat", -1).asInt();
		}

		if (root.isMember("turnServer"))
		{
			auto turnServerNode = root.get("turnServer", NULL);
			if (turnServerNode.isMember("provider"))
			{
				auto turnCredProviderUri = turnServerNode.get("provider", "").asString();
				if (!turnCredProviderUri.empty())
				{
					turn_provider_.reset(new TurnCredentialProvider(turnCredProviderUri));
					turn_provider_->SignalCredentialsRetrieved.connect(this, &Initializer::OnCredentialsRetrieved);
				}
			}
		}

		if (root.isMember("authentication"))
		{
			auto authenticationNode = root.get("authentication", NULL);
			ServerAuthenticationProvider::ServerAuthInfo authInfo;

			if (authenticationNode.isMember("resource"))
			{
				authInfo.resource = authenticationNode.get("resource", "").asString();
			}

			if (authenticationNode.isMember("clientId"))
			{
				authInfo.clientId = authenticationNode.get("clientId", "").asString();
			}

			if (authenticationNode.isMember("clientSecret"))
			{
				authInfo.clientSecret = authenticationNode.get("clientSecret", "").asString();
			}

			if (authenticationNode.isMember("authority"))
			{
				authInfo.authority = authenticationNode.get("authority", "").asString();
				auth_provider_.reset(new ServerAuthenticationProvider(authInfo));
				auth_provider_->SignalAuthenticationComplete.connect(this, &Initializer::OnAuthenticationComplete);

				if (turn_provider_.get() != nullptr)
				{
					turn_provider_->SetAuthenticationProvider(auth_provider_.get());
				}
			}
		}
	}

	// trigger auth
	if (auth_provider_.get() != nullptr)
	{
		state_ = State::AUTHENTICATING;
		if (!auth_provider_->Authenticate())
		{
			state_ = State::NONE;
		}
	}
	else
	{
		// bypass auth AND credentials (since creds query needs auth)
		state_ = State::INITIALIZED;
		OnInitialized();
	}
}

void Initializer::OnAuthenticationComplete(const AuthenticationProviderResult& creds)
{
	if (state_ != State::AUTHENTICATING)
	{
		return;
	}

	if (!creds.successFlag)
	{
		state_ = State::NONE;
		OnError("authentication request failed");
	}
	
	access_token_ = creds.accessToken;

	if (turn_provider_.get() == nullptr)
	{
		// bypass turn creds
		state_ = State::INITIALIZED;
		OnInitialized();
		return;
	}

	state_ = State::GETTING_TURN_CREDS;
	if (!turn_provider_->RequestCredentials())
	{
		state_ = State::NONE;
		OnError("TurnCredential request could not be issued");
	}
}

void Initializer::OnCredentialsRetrieved(const TurnCredentials& creds)
{
	if (state_ != State::GETTING_TURN_CREDS)
	{
		return;
	}

	if (!creds.successFlag)
	{
		state_ = State::NONE;
		OnError("TurnCredential request failed");
	}

	turn_username_ = creds.username;
	turn_password_ = creds.password;

	state_ = State::INITIALIZED;
	OnInitialized();
}

void Initializer::OnInitialized()
{
	if (state_ != State::INITIALIZED)
	{
		return;
	}

	InitializedValues values;
	values.serverAddress = server_address_;
	values.serverPort = server_port_;
	values.heartbeatIntervalMs = heartbeat_interval_;
	values.accessToken = access_token_;
	values.turnUsername = turn_username_;
	values.turnPassword = turn_password_;
	
	on_run_(values);

	// indicate the execution is complete
	completion_event_.Set();
}

void Initializer::OnError(const std::string& errorDesc)
{
	LOG(LERROR) << __FUNCTION__ << errorDesc;

	// TODO(bengreenier): support other platforms
#ifdef WEBRTC_WIN
	MessageBoxA(0, errorDesc.c_str(), "Initializer - Error", MB_OK | MB_ICONERROR);
#endif
}

InitializerWrapper::InitializerWrapper(const std::string& configPath,
	std::function<void(Initializer::InitializedValues)> onRun) : config_path_(configPath), on_run_(onRun)
{
}

InitializerWrapper::~InitializerWrapper()
{
	if (thread_.get() != nullptr)
	{
		thread_->join();
	}
}

void InitializerWrapper::Run()
{
	rtc::Thread* procThread = nullptr;
	rtc::Event threadRunning(false, false);
	thread_.reset(new std::thread([&] {

		// TODO(bengreenier): support other platforms
#ifdef WEBRTC_WIN
		rtc::Win32Thread w32_thread;
		procThread = &w32_thread;
#endif

		rtc::ThreadManager::Instance()->SetCurrentThread(procThread);

		instance_.reset(new Initializer(config_path_, on_run_));
		instance_->Run();

		threadRunning.Set();

		procThread->ProcessMessages(w32_thread.kForever);
	}));
	threadRunning.Wait(rtc::Event::kForever);
	instance_->WaitForCompletion();
	procThread->Stop();
}