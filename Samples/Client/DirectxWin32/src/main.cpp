#include "pch.h"

#include <stdlib.h>
#include <shellapi.h>
#include <fstream>
#include <functional>

#include "webrtc.h"
#include "third_party/jsoncpp/source/include/json/json.h"

#include "oauth24d_provider.h"

//--------------------------------------------------------------------------------------
// Required app libs
//--------------------------------------------------------------------------------------
#pragma comment(lib, "d3dcompiler.lib")
#pragma comment(lib, "dxguid.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "imm32.lib")
#pragma comment(lib, "version.lib")
#pragma comment(lib, "usp10.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "winmm.lib")

//--------------------------------------------------------------------------------------
// Global Methods
//--------------------------------------------------------------------------------------
std::string GetAbsolutePath(std::string fileName)
{
	TCHAR buffer[MAX_PATH];
	GetModuleFileName(NULL, buffer, MAX_PATH);
	char charPath[MAX_PATH];
	wcstombs(charPath, buffer, wcslen(buffer) + 1);

	std::string::size_type pos = std::string(charPath).find_last_of("\\/");
	return std::string(charPath).substr(0, pos + 1) + fileName;
}

//--------------------------------------------------------------------------------------
// WebRTC
//--------------------------------------------------------------------------------------
int InitWebRTC(char* server, int port, int heartbeat, char* authCodeUri, char* authPollUri)
{
	rtc::EnsureWinsockInit();
	rtc::Win32Thread w32_thread;
	rtc::ThreadManager::Instance()->SetCurrentThread(&w32_thread);

	DefaultMainWindow wnd(server, port, FLAG_autoconnect, FLAG_autocall, false, 1280, 720);

	if (!wnd.Create())
	{
		RTC_NOTREACHED();
		return -1;
	}

	rtc::InitializeSSL();
	OAuth24DProvider oauth(authCodeUri, authPollUri);

	OAuth24DProvider::CodeCompleteCallback codeComplete([&](const OAuth24DProvider::CodeData& data) {
		std::wstring wcode(data.user_code.begin(), data.user_code.end());
		
		// set the code
		wnd.SetAuthCode(wcode);

		// redraw the ui that shows the code only if we're currently in that ui
		if (wnd.current_ui() == DefaultMainWindow::UI::CONNECT_TO_SERVER)
		{
			wnd.SwitchToConnectUI();
		}
	});
	oauth.SignalCodeComplete.connect(&codeComplete, &OAuth24DProvider::CodeCompleteCallback::Handle);
	
	PeerConnectionClient client;

	client.SetHeartbeatMs(heartbeat);

	AuthenticationProvider::AuthenticationCompleteCallback authComplete([&](const AuthenticationProviderResult& data) {
		if (data.successFlag)
		{
			client.SetAuthorizationHeader("Bearer " + data.accessToken);

			// set the code value to (OK) communicating auth is complete
			wnd.SetAuthCode(L"OK");

			// redraw the ui that shows the code only if we're currently in that ui
			if (wnd.current_ui() == DefaultMainWindow::UI::CONNECT_TO_SERVER)
			{
				wnd.SwitchToConnectUI();
			}
		}
	});
	oauth.SignalAuthenticationComplete.connect(&authComplete, &AuthenticationProvider::AuthenticationCompleteCallback::Handle);

	// TODO(bengreenier): handle failure here
	oauth.Authenticate();

	rtc::scoped_refptr<Conductor> conductor(
		new rtc::RefCountedObject<Conductor>(&client, &wnd));

	// Main loop.
	MSG msg;
	BOOL gm;
	while ((gm = ::GetMessage(&msg, NULL, 0, 0)) != 0 && gm != -1)
	{
		if (!wnd.PreTranslateMessage(&msg))
		{
			::TranslateMessage(&msg);
			::DispatchMessage(&msg);
		}
	}

	rtc::CleanupSSL();

	return 0;
}

//--------------------------------------------------------------------------------------
// Entry point to the program. Initializes everything and goes into a message processing 
// loop. Idle time is used to render the scene.
//--------------------------------------------------------------------------------------
int WINAPI wWinMain(
	_In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE hPrevInstance,
	_In_ LPWSTR lpCmdLine,
	_In_ int nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);

	int nArgs;
	char server[1024];
	strcpy(server, FLAG_server);
	char authCodeUri[1024];
	strcpy(authCodeUri, FLAG_authCodeUri);
	char authPollUri[1024];
	strcpy(authPollUri, FLAG_authPollUri);
	int port = FLAG_port;
	int heartbeat = FLAG_heartbeat;
	LPWSTR* szArglist = CommandLineToArgvW(lpCmdLine, &nArgs);

	// Try parsing command line arguments.
	if (szArglist && nArgs == 2)
	{
		wcstombs(server, szArglist[0], sizeof(server));
		port = _wtoi(szArglist[1]);
	}
	else // Try parsing config file.
	{
		std::string configFilePath = GetAbsolutePath("webrtcConfig.json");
		std::ifstream configFile(configFilePath);
		Json::Reader reader;
		Json::Value root = NULL;
		if (configFile.good())
		{
			reader.parse(configFile, root, true);
			if (root.isMember("server"))
			{
				strcpy(server, root.get("server", FLAG_server).asCString());
			}

			if (root.isMember("port"))
			{
				port = root.get("port", FLAG_port).asInt();
			}

			if (root.isMember("heartbeat"))
			{
				heartbeat = root.get("heartbeat", FLAG_heartbeat).asInt();
			}

			if (root.isMember("authentication"))
			{
				auto authenticationWrapper = root.get("authentication", NULL);
				if (authenticationWrapper != NULL)
				{
					if (authenticationWrapper.isMember("codeUri"))
					{
						strcpy(authCodeUri, authenticationWrapper.get("codeUri", FLAG_authCodeUri).asCString());
					}

					if (authenticationWrapper.isMember("pollUri"))
					{
						strcpy(authPollUri, authenticationWrapper.get("pollUri", FLAG_authPollUri).asCString());
					}
				}
			}
		}
	}

	return InitWebRTC(server, port, heartbeat, authCodeUri, authPollUri);
}
