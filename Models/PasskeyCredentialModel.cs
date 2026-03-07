namespace CryptostellerAPI.Models
{
    public class PasskeyCredentialModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = default!;
        public string CredentialId { get; set; } = default!;  // base64url, from device
        public string PublicKey { get; set; } = default!;  // COSE public key, base64url
        public long SignCount { get; set; } = 0;        
        public string FriendlyName { get; set; } = "Cryptosteller";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
