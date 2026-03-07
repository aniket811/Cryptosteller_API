namespace CryptostellerAPI.Models
{
    public class RegisterVerifyResponse
    {
        public bool Success { get; set; }
        public string? CredentialId { get; set; }
        public string? Message { get; set; }
    }
    public class AuthVerifyResponse
    {
        public bool Success { get; set; }
        public string? CredentialId { get; set; }
        public string? Message { get; set; }
    }
}
