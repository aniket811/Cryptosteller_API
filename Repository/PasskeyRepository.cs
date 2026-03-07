using CryptostellerAPI.Data;
using CryptostellerAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptostellerAPI.Repository
{
    public class PasskeyRepository : IPasskeyRepository
    {
        private readonly ApplicationDbContext _db;

        public PasskeyRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddCredentialAsync(PasskeyCredentialModel credential)
        {
            _db.PasskeyCredentials.Add(credential);
            await _db.SaveChangesAsync();
        }

        public async Task DeactivateAsync(Guid id, string userId)
        {
            var entity = await _db.PasskeyCredentials.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (entity is null) return;
            entity.IsActive = false;
            await _db.SaveChangesAsync();
        }

        public async Task<List<PasskeyCredentialModel>> GetActiveByUserIdAsync(string userId)
        {
            return await _db.PasskeyCredentials
                .Where(p => p.UserId == userId && p.IsActive)
                .ToListAsync();
        }
        public async Task UpdateAfterLoginAsync(string credentialId, uint newSignCount)
        {
            await _db.PasskeyCredentials
                .Where(c => c.CredentialId == credentialId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.SignCount, newSignCount)
                    .SetProperty(c => c.LastUsedAt, DateTime.UtcNow));
        }
        public async Task<PasskeyCredentialModel?> GetByCredentialIdAsync(string credentialId)
        {
            return await _db.PasskeyCredentials
                .FirstOrDefaultAsync(c => c.CredentialId == credentialId && c.IsActive);
        }
        public async Task UpdateSignCountAsync(string credentialId, long newSignCount)
        {
            var entity = await _db.PasskeyCredentials.FirstOrDefaultAsync(p => p.CredentialId == credentialId && p.IsActive);
            if (entity is null) return;
            entity.SignCount = newSignCount;
            entity.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
