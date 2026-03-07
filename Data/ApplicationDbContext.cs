using CryptostellerAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptostellerAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PasskeyCredentialModel> PasskeyCredentials { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PasskeyCredentialModel>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.UserId).IsRequired();
                b.Property(p => p.CredentialId).IsRequired();
                b.Property(p => p.PublicKey).IsRequired(false);
                b.Property(p => p.SignCount).IsRequired();
                b.Property(p => p.FriendlyName).HasMaxLength(256);
                b.Property(p => p.CreatedAt).IsRequired();
            });
        }
    }
}
