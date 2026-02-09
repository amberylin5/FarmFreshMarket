using FarmFreshMarket.ViewModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmFreshMarket.Models
{
    public class AuthDbContext : IdentityDbContext
    {
        private readonly IConfiguration _configuration;

        public AuthDbContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DbSet<Member> Members { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<PasswordHistory> PasswordHistories { get; set; } // ✅ ADD THIS
        public DbSet<PasswordPolicy> PasswordPolicies { get; set; }
        public DbSet<TwoFactorCode> TwoFactorCodes { get; set; }
        public DbSet<User2FASettings> User2FASettings { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = _configuration.GetConnectionString("AuthConnectionString");
            optionsBuilder.UseSqlServer(connectionString);
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Index for faster lookups
            builder.Entity<TwoFactorCode>()
                .HasIndex(c => new { c.UserId, c.Code, c.IsUsed, c.ExpiresAt })
                .HasDatabaseName("IX_TwoFactorCode_UserId_Code");

            builder.Entity<User2FASettings>()
                .HasIndex(s => s.UserId)
                .IsUnique();
        }
    }
}
