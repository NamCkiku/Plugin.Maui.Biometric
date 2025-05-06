namespace BA_Mobile.Biometric;

public class AuthenticationResponse
{
    public BiometricResponseStatus Status { get; set; }
    public AuthenticationType AuthenticationType { get; set; }
    public string ErrorMsg { get; set; }
}