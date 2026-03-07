using CryptostellerAPI.Models;

namespace CryptostellerAPI.Repository
{
    public interface IPasskeyRepository
    {
        /// <summary>Save a new credential after successful registration.</summary>
        Task AddCredentialAsync(PasskeyCredentialModel credential);

        /// <summary>Find credential by the ID the device returned.</summary>
        Task<PasskeyCredentialModel?> GetByCredentialIdAsync(string credentialId);

        /// <summary>All active passkeys for a user (to exclude on re-registration).</summary>
        Task<List<PasskeyCredentialModel>> GetActiveByUserIdAsync(string userId);

        /// <summary>Update sign counter + last used timestamp after each login.</summary>
        Task UpdateSignCountAsync(string credentialId, long newSignCount);

        /// <summary>Soft-delete a passkey (user removes it from their account).</summary>
        Task DeactivateAsync(Guid id, string userId);
        Task UpdateAfterLoginAsync(string credentialId, uint newSignCount);
    }
}
