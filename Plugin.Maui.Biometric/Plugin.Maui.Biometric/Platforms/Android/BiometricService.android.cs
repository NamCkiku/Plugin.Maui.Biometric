using Android;
using Android.App;
using Android.Content.PM;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Activity = AndroidX.AppCompat.App.AppCompatActivity;
using Application = Android.App.Application;
using BiometricPrompt = AndroidX.Biometric.BiometricPrompt;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace BA_Mobile.Biometric;

internal partial class BiometricService
{
    private readonly BiometricManager biometricManager = BiometricManager.From(Application.Context);
    public partial Task<BiometricHwStatus> GetAuthenticationStatusAsync(AuthenticatorStrength authStrength)
    {
        if (Platform.CurrentActivity is not Activity activity)
        {
            return Task.FromResult(BiometricHwStatus.Failure);
        }

        var strength = authStrength.Equals(AuthenticatorStrength.Strong) ?
            BiometricManager.Authenticators.BiometricStrong :
            BiometricManager.Authenticators.BiometricWeak;

        var value = biometricManager.CanAuthenticate(strength);
        var response = value switch
        {
            BiometricManager.BiometricSuccess => BiometricHwStatus.Success,
            BiometricManager.BiometricErrorNoHardware => BiometricHwStatus.NoHardware,
            BiometricManager.BiometricErrorHwUnavailable => BiometricHwStatus.Unavailable,
            BiometricManager.BiometricErrorNoneEnrolled => BiometricHwStatus.NotEnrolled,
            BiometricManager.BiometricErrorUnsupported => BiometricHwStatus.Unsupported,
            _ => BiometricHwStatus.Failure,
        };

        return Task.FromResult(response);
    }

    public partial async Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request, CancellationToken token)
    {
        try
        {
            if (Platform.CurrentActivity is not Activity activity)
            {
                //This case should be logically unreachable but adding this just
                //In case for some reason Platform's CurrentActivity decides to flip with me.
                return new AuthenticationResponse
                {
                    Status = BiometricResponseStatus.Failure,
                    ErrorMsg = "Your Platform.CurrentActivity either returned null or is not of type `AndroidX.AppCompat.App.AppCompatActivity`, ensure your Activity is of the right type and that its not null when you call this method"
                };
            }

            var strength = request.AuthStrength.Equals(AuthenticatorStrength.Strong) ?
               BiometricManager.Authenticators.BiometricStrong :
               BiometricManager.Authenticators.BiometricWeak;

            var allInfo = new BiometricPrompt.PromptInfo.Builder()
                    .SetTitle(request.Title)
                    .SetSubtitle(request.Subtitle)
                    .SetDescription(request.Description);

            if (request.AllowPasswordAuth)
            {
                allInfo.SetAllowedAuthenticators(strength | BiometricManager.Authenticators.DeviceCredential);
            }
            else
            {
                allInfo.SetNegativeButtonText(request.NegativeText);
                allInfo.SetAllowedAuthenticators(strength);
            }

            var promptInfo = allInfo.Build();
            var executor = ContextCompat.GetMainExecutor(activity);
            var authCallback = new AuthCallback()
            {
                Response = new TaskCompletionSource<AuthenticationResponse>()
            };

            var biometricPrompt = new BiometricPrompt(activity, executor, authCallback);

            await using (token.Register(() => biometricPrompt.CancelAuthentication()))
            {
                biometricPrompt.Authenticate(promptInfo);
                var response = await authCallback.Response.Task;
                return response;
            }
        }
        catch (Exception ex)
        {
            return new AuthenticationResponse
            {
                Status = BiometricResponseStatus.Failure,
                ErrorMsg = ex.Message + ex.StackTrace
            };
        }
    }

    public partial async Task<List<BiometricType>> GetEnrolledBiometricTypesAsync()
    {
        var availableOptions = new List<BiometricType>();
        if (Platform.CurrentActivity is Activity activity)
        {
            var availability = await GetAvailabilityAsync(
            strength: AuthenticatorStrength.Weak).ConfigureAwait(false);
            if (availability is Availability.NoBiometric or
                                Availability.NoPermission or
                                Availability.Available)
            {
                availableOptions.Add(BiometricType.Fingerprint);
            }
        }
        return availableOptions;
    }

    private Task<Availability> GetAvailabilityAsync(
       AuthenticatorStrength strength = AuthenticatorStrength.Weak)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return Task.FromResult(Availability.NoApi);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(23) &&
            !OperatingSystem.IsAndroidVersionAtLeast(28) &&
            Application.Context.CheckCallingOrSelfPermission(Manifest.Permission.UseFingerprint) != Permission.Granted)
        {
            return Task.FromResult(Availability.NoPermission);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(28) &&
            Application.Context.CheckCallingOrSelfPermission(Manifest.Permission.UseBiometric) != Permission.Granted)
        {
            return Task.FromResult(Availability.NoPermission);
        }

        var canAuthenticate = biometricManager.CanAuthenticate(GetAllowedAuthenticators(strength));
        var availability = canAuthenticate switch
        {
            BiometricManager.BiometricErrorNoHardware => Availability.NoSensor,
            BiometricManager.BiometricErrorHwUnavailable => Availability.Unknown,
            BiometricManager.BiometricErrorNoneEnrolled => Availability.NoBiometric,
            BiometricManager.BiometricSuccess => Availability.Available,
            _ => Availability.Unknown,
        };
        if (availability == Availability.Available ||
            strength is not AuthenticatorStrength.Any)
        {
            return Task.FromResult(availability);
        }

        try
        {
            if (Application.Context.GetSystemService(
                    name: Android.Content.Context.KeyguardService) is not KeyguardManager manager)
            {
                return Task.FromResult(Availability.NoFallback);
            }

            return Task.FromResult(manager.IsDeviceSecure
                ? Availability.Available
                : Availability.NoFallback);
        }
        catch
        {
            return Task.FromResult(Availability.NoFallback);
        }
    }

    private static int GetAllowedAuthenticators(AuthenticatorStrength strength)
    {
        return strength switch
        {
            AuthenticatorStrength.Strong => BiometricManager.Authenticators.BiometricStrong,
            AuthenticatorStrength.Weak => BiometricManager.Authenticators.BiometricStrong |
                                           BiometricManager.Authenticators.BiometricWeak,
            AuthenticatorStrength.Any => BiometricManager.Authenticators.BiometricStrong |
                                          BiometricManager.Authenticators.BiometricWeak |
                                          BiometricManager.Authenticators.DeviceCredential,
            _ => throw new ArgumentOutOfRangeException(nameof(strength), strength, null)
        };

    }

    private static partial bool GetIsPlatformSupported() => true;
}