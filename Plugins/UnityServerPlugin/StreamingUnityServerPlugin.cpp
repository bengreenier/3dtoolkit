#define SUPPORT_D3D11 1
#define WEBRTC_WIN 1
#define SHOW_CONSOLE 0

#include <iostream>
#include <thread>
#include <string>
#include <cstdint>
#include <wrl.h>
#include <d3d11_2.h>
#include "IUnityGraphicsD3D11.h"
#include "IUnityGraphics.h"
#include "IUnityInterface.h"

#include "webrtc/base/checks.h"
#include "webrtc/base/ssladapter.h"
#include "webrtc/modules/video_coding/codecs/h264/h264_encoder_impl.h"

#include "video_helper.h"
#include "conductor.h"
#include "default_main_window.h"
#include "flagdefs.h"
#include "peer_connection_client.h"
#include "initializer.h"

#pragma warning( disable : 4100 )
#pragma comment(lib, "ws2_32.lib") 

#pragma comment(lib, "d3dcompiler.lib")
#pragma comment(lib, "dxguid.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "imm32.lib")
#pragma comment(lib, "version.lib")
#pragma comment(lib, "usp10.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "winmm.lib")

#pragma comment(lib, "dmoguids.lib")
#pragma comment(lib, "wmcodecdspuuid.lib")
#pragma comment(lib, "secur32.lib")
#pragma comment(lib, "msdmo.lib")
#pragma comment(lib, "strmiids.lib")

#pragma comment(lib, "common_video.lib")
#pragma comment(lib, "webrtc.lib")
#pragma comment(lib, "boringssl_asm.lib")
#pragma comment(lib, "field_trial_default.lib")
#pragma comment(lib, "metrics_default.lib")
#pragma comment(lib, "protobuf_full.lib")

using namespace Microsoft::WRL;
using namespace Toolkit3DLibrary;

void(__stdcall*s_onInputUpdate)(const char *msg);
void(__stdcall*s_onLog)(const char* msg);

DEFINE_GUID(IID_Texture2D, 0x6f15aaf2, 0xd208, 0x4e89, 0x9a, 0xb4, 0x48, 0x95, 0x35, 0xd3, 0x4f, 0x9c);

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;
static UnityGfxRenderer s_DeviceType = kUnityGfxRendererNull;
static ComPtr<ID3D11Device> s_Device;
static ComPtr<ID3D11DeviceContext> s_Context;

static rtc::scoped_refptr<Conductor> s_conductor = nullptr;

VideoHelper*				g_videoHelper = nullptr;
static ID3D11Texture2D*		s_frameBuffer = nullptr;
static ID3D11Texture2D*		s_frameBufferCopy = nullptr;

DefaultMainWindow *wnd;
std::thread *messageThread;

std::string s_server = "signalingserveruri";
uint32_t s_port = 3000;
int s_heartbeat = 5000;
bool s_closing = false;

void LogUnity(const std::string& message)
{
	if (s_onLog)
	{
		LOG(INFO) << message;
		(*s_onLog)(message.c_str());
	}
}

void FrameUpdate()
{
}

// Handles input from client.
void InputUpdate(const std::string& message)
{
	if (s_onInputUpdate)
	{
		LOG(INFO) << message;

		(*s_onInputUpdate)(message.c_str());
	}
}

void InitWebRTC()
{
	InitializerWrapper application(webrtc::ExePath("webrtcConfig.json"), [&](Initializer::InitializedValues values)
	{
		if (values.serverAddress.empty())
		{
			values.serverAddress = s_server;
		}

		if (values.serverPort == -1)
		{
			values.serverPort = s_port;
		}

		LogUnity("init got values " + values.serverAddress + ":" + std::to_string(values.serverPort));

		PeerConnectionClient client;

		if (!values.accessToken.empty())
		{
			client.SetAuthorizationHeader("Bearer " + values.accessToken);
		}

		if (values.heartbeatIntervalMs != -1)
		{
			client.SetHeartbeatMs(values.heartbeatIntervalMs);
		}

		wnd = new DefaultMainWindow(values.serverAddress.c_str(), values.serverPort, FLAG_autoconnect, FLAG_autocall,
			true, 1280, 720);

		wnd->Create();

		s_conductor = new rtc::RefCountedObject<Conductor>(&client, wnd, &FrameUpdate, &InputUpdate, g_videoHelper);

		if (!values.turnUsername.empty() && !values.turnPassword.empty())
		{
			s_conductor->SetTurnCredentials(values.turnUsername, values.turnPassword);
		}

		if (s_conductor != nullptr)
		{
			MainWindowCallback *callback = s_conductor;

			callback->StartLogin(s_server, s_port);
		}

		// Main loop.
		MSG msg;
		BOOL gm;
		while ((gm = ::GetMessage(&msg, NULL, 0, 0)) != 0 && gm != -1 && !s_closing)
		{
			if (!wnd->PreTranslateMessage(&msg))
			{
				try
				{
					::TranslateMessage(&msg);
					::DispatchMessage(&msg);
				}
				catch (const std::exception& e) { // reference to the base of a polymorphic object
					std::cout << e.what(); // information from length_error printed
				}
			}
		}
	});

	application.Run();
}

static void UNITY_INTERFACE_API OnEncode(int eventID)
{
	LogUnity("OnEncode");

	if (s_Context)
    {
		LogUnity("OnEncode::s_Context");

		if (s_frameBuffer == nullptr)
		{
			LogUnity("OnEncode::s_frameBuffer");

			ID3D11RenderTargetView* rtv(nullptr);
			ID3D11DepthStencilView* depthStencilView(nullptr);

			s_Context->OMGetRenderTargets(1, &rtv, &depthStencilView);
			
			if (rtv)
			{
				LogUnity("OnEncode::rtv");

				rtv->GetResource(reinterpret_cast<ID3D11Resource**>(&s_frameBuffer));
				rtv->Release();

				D3D11_TEXTURE2D_DESC desc;

				s_frameBuffer->GetDesc(&desc);
					
				g_videoHelper->Initialize(s_frameBuffer, desc.Format, desc.Width, desc.Height);
				
				s_frameBuffer->Release();
				
				messageThread = new std::thread(InitWebRTC);

				LogUnity("created thread");
			}
		}

        return;
    }
}

extern "C" void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    switch (eventType)
    {
        case kUnityGfxDeviceEventInitialize:
        {
            s_DeviceType = s_Graphics->GetRenderer();
            s_Device = s_UnityInterfaces->Get<IUnityGraphicsD3D11>()->GetDevice();
            s_Device->GetImmediateContext(&s_Context);
			
			LogUnity("kUnityGfxDeviceEventInitialize fired");

			break;
        }

        case kUnityGfxDeviceEventShutdown:
        {
			s_Context.Reset();
            s_Device.Reset();
            s_DeviceType = kUnityGfxRendererNull;
			
			LogUnity("kUnityGfxDeviceEventShutdown fired");

            break;
        }
    }
}

extern "C" void	UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
#if SHOW_CONSOLE
    AllocConsole();
    FILE* out(nullptr);
    freopen_s(&out, "CONOUT$", "w", stdout);

    std::cout << "Console open..." << std::endl;
#endif

    s_UnityInterfaces = unityInterfaces;
    s_Graphics = s_UnityInterfaces->Get<IUnityGraphics>();
    s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

    // Run OnGraphicsDeviceEvent(initialize) manually on plugin load
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);

	// Creates and initializes the video helper library.
	g_videoHelper = new VideoHelper(s_Device.Get(), s_Context.Get());
}

extern "C" __declspec(dllexport) void Close()
{
	if (s_conductor != nullptr)
	{
		MainWindowCallback *callback = s_conductor;
		callback->DisconnectFromCurrentPeer();
		callback->DisconnectFromServer();

		callback->Close();

		s_closing = true;
		messageThread->join();

		s_conductor = nullptr;
	}
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);

	Close();
}

extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc()
{
    return OnEncode;
}

extern "C" __declspec(dllexport) void SetInputDataCallback(void(__stdcall*onInputUpdate)(const char *msg))
{
	s_onInputUpdate = onInputUpdate;
}

extern "C" __declspec(dllexport) void SetLoggingCallback(void(_stdcall*onLog)(const char* msg))
{
	s_onLog = onLog;

	LogUnity("set logging callback");
}
