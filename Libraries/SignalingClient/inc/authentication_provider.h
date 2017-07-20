#pragma once

#include <string>
#include <functional>

/// <summary>
/// Represents a result from an authentication provider authenticate operation
/// </summary>
struct AuthenticationProviderResult
{
public:
	bool successFlag;
	std::string accessToken;

};

/// <summary>
/// Base class that represents an authentication provider
/// </summary>
class AuthenticationProvider
{
public:
	AuthenticationProvider() {}

	sigslot::signal1<const AuthenticationProviderResult&> SignalAuthenticationComplete;

	virtual bool Authenticate() = 0;

protected:
	virtual ~AuthenticationProvider() {}
};
