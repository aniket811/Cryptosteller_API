namespace CryptostellerAPI.Models
{
    public class RegisterOptionsResponse
    {
        
            public string ChallengeId { get; set; }
            public string Challenge { get; set; }
            public string RpId { get; set; }
            public string RpName { get; set; }
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string DisplayName { get; set; }
            public List<ExcludeCredentialDto> ExcludeCredentials { get; set; }
        
    }
    public record ExcludeCredentialDto(string Id, string Type);
}
