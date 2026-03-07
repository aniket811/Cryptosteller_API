namespace CryptostellerAPI.Models
{
    public class PasskeyDTOs
    {
        public class StoredChallenge
        {
            public string ChallengeId { get; set; }
            public string ChallengeBytes { get; set; }
            public string UserId { get; set; }
            public ChallengeType Type { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsUsed { get; set; } = false;
        }

        public enum ChallengeType { Registration, Authentication }
    }
}
