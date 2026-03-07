namespace CryptostellerAPI.Models
{
    public class AuthOptionsResponse
    {
        public string ChallengeId { get; set; } = default!;
        public string Challenge { get; set; } = default!;
        public string RpId { get; set; } = default!;
        public int Timeout { get; set; } = 60000;
        public List<AllowCredentialDto> AllowCredentials { get; set; } = [];
    }

    public record AllowCredentialDto(string Id, string Type);
}
