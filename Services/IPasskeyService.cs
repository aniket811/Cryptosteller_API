using CryptostellerAPI.Models;

namespace CryptostellerAPI.Services
{
    public interface IPasskeyService
    {
        public Task<RegisterOptionsResponse> GenerateRegistrationOptionsAsync(string firebaseUid, string email, string displayName);
        public Task<RegisterVerifyResponse> VerifyRegistrationAsync(string firebaseUid, string email, string displayName, RegisterVerifyRequest request);
        public Task<AuthOptionsResponse> GenerateAuthOptionsAsync(AuthOptionsRequest request);
        public Task<AuthVerifyResponse> VerifyAuthenticationAsync(AuthVerifyRequest request);

    }
}
