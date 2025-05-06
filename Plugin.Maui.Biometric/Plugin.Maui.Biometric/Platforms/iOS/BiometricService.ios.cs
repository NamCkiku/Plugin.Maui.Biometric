using Foundation;
using LocalAuthentication;
using ObjCRuntime;
using System.Globalization;

namespace BA_Mobile.Biometric;

internal partial class BiometricService
{
    public partial Task<BiometricHwStatus> GetAuthenticationStatusAsync(AuthenticatorStrength authStrength)
    {
        var localAuthContext = new LAContext();
        if (localAuthContext.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out var _))
        {
            if (localAuthContext.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var _))
            {
                if (localAuthContext.BiometryType != LABiometryType.None)
                {
                    return Task.FromResult(BiometricHwStatus.Success);
                }

                return Task.FromResult(BiometricHwStatus.NotEnrolled);
            }

            return Task.FromResult(BiometricHwStatus.NotEnrolled);
        }

        return Task.FromResult(BiometricHwStatus.Failure);
    }

    public async partial Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request, CancellationToken token)
    {
        var response = new AuthenticationResponse();
        var context = new LAContext();
        LAPolicy policy = request.AllowPasswordAuth ? LAPolicy.DeviceOwnerAuthentication : LAPolicy.DeviceOwnerAuthenticationWithBiometrics;
        if (context.CanEvaluatePolicy(policy, out NSError _))
        {
            var callback = await context.EvaluatePolicyAsync(policy, request.Title);
            response.Status = callback.Item1 ? BiometricResponseStatus.Success : BiometricResponseStatus.Failure;
            response.AuthenticationType = AuthenticationType.Unknown;
            response.ErrorMsg = callback.Item2?.ToString();
        };
        return response;
    }

    public async partial Task<List<BiometricType>> GetEnrolledBiometricTypesAsync()
    {
        var availableOptions = new List<BiometricType>();

        var localAuthContext = new LAContext();
        // we need to call this, because it will always return none, if you don't call CanEvaluatePolicy
        var availability = await GetAvailabilityAsync(
            strength: AuthenticatorStrength.Weak).ConfigureAwait(false);

        // iOS 11+
        if (localAuthContext.RespondsToSelector(new Selector("biometryType")))
        {
            var type = localAuthContext.BiometryType switch
            {
                LABiometryType.None => BiometricType.None,
                LABiometryType.TouchId => BiometricType.Fingerprint,
                LABiometryType.FaceId => BiometricType.Face,
                _ => BiometricType.None,
            };
            availableOptions.Add(type);
        }
        else
        {
            // iOS < 11
            if (availability is Availability.NoApi or
                                Availability.NoSensor or
                                Availability.Unknown)
            {
                availableOptions.Add(BiometricType.None);
            }
            else
            {
                availableOptions.Add(BiometricType.Fingerprint);
            }
        }
        
        return availableOptions;
    }

    public Task<Availability> GetAvailabilityAsync(
        AuthenticatorStrength strength = AuthenticatorStrength.Weak)
    {
        var _context = new LAContext();
        if (_context == null)
            return Task.FromResult(Availability.NoApi);

        var policy = GetPolicy(strength);
        if (_context.CanEvaluatePolicy(policy, out var error))
            return Task.FromResult(Availability.Available);

        switch ((LAStatus)(int)error.Code)
        {
            case LAStatus.BiometryNotAvailable:
                return Task.FromResult(IsDeniedError(error) ?
                    Availability.Denied :
                    Availability.NoSensor);
            case LAStatus.BiometryNotEnrolled:
                return Task.FromResult(Availability.NoBiometric);
            case LAStatus.PasscodeNotSet:
                return Task.FromResult(Availability.NoFallback);
            default:
                return Task.FromResult(Availability.Unknown);
        }
    }

    private static LAPolicy GetPolicy(AuthenticatorStrength strength)
    {
        return strength switch
        {
            AuthenticatorStrength.Any => LAPolicy.DeviceOwnerAuthentication,
            _ => LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
        };
    }

    private static bool IsDeniedError(NSError error)
    {
        if (!string.IsNullOrEmpty(error.Description))
        {
            // we might have some issues, if the error gets localized :/
#pragma warning disable CA1308
            return error.Description.ToLower(CultureInfo.InvariantCulture).Contains("denied", StringComparison.OrdinalIgnoreCase);
#pragma warning restore CA1308
        }

        return false;
    }

    private static partial bool GetIsPlatformSupported() => true;
}