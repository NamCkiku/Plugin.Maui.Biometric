namespace BA_Mobile.Biometric;

public interface IBiometric
{
    Task<BiometricHwStatus> GetAuthenticationStatusAsync(AuthenticatorStrength authStrength = AuthenticatorStrength.Strong);

    Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request, CancellationToken token);

    Task<List<BiometricType>> GetEnrolledBiometricTypesAsync();

    bool IsPlatformSupported { get; }
}