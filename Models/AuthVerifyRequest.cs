namespace CryptostellerAPI.Models
{
    public class AuthVerifyRequest
    {
        
            public string ChallengeId { get; set; } = default!;
            public string Id { get; set; } = default!;
            public string RawId { get; set; } = default!;
            public string ClientDataJSON { get; set; } = default!;
            public string AuthenticatorData { get; set; } = default!;
            public string Signature { get; set; } = default!;
            public string? UserHandle { get; set; }
        
    }
}
