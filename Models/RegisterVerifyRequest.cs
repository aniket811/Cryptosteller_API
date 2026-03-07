namespace CryptostellerAPI.Models
{
    public class RegisterVerifyRequest
    {
        // Attestation response fields returned by the browser
        public string Id { get; set; } = default!; // base64url
        public string RawId { get; set; } = default!; // base64url
        public string AttestationObject { get; set; } = default!; // base64url
        public string ClientDataJSON { get; set; } = default!; // base64url

        // Optional friendly name for the passkey
        public string? FriendlyName { get; set; }

        // The challengeId created earlier so we can look up the server-side challenge
        public string ChallengeId { get; set; } = default!;
    }
}
