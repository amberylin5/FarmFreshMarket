using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmFreshMarket.Migrations
{
    public partial class SMS : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Simple: Just ensure default values
            migrationBuilder.Sql(@"
                -- Update NULL values to defaults if table exists
                IF OBJECT_ID('User2FASettings', 'U') IS NOT NULL
                BEGIN
                    UPDATE User2FASettings SET PreferredMethod = 'Email' WHERE PreferredMethod IS NULL;
                    UPDATE User2FASettings SET Email = '' WHERE Email IS NULL;
                    UPDATE User2FASettings SET PhoneNumber = '' WHERE PhoneNumber IS NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to rollback
        }
    }
}