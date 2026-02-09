using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmFreshMarket.Migrations
{
    public partial class FixDeliveryMethodNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, update any NULL values to 'Email'
            migrationBuilder.Sql(@"
                UPDATE [dbo].[TwoFactorCodes] 
                SET [DeliveryMethod] = 'Email' 
                WHERE [DeliveryMethod] IS NULL
            ");

            // Then make the column NOT NULL with a default value
            migrationBuilder.AlterColumn<string>(
                name: "DeliveryMethod",
                table: "TwoFactorCodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Email",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DeliveryMethod",
                table: "TwoFactorCodes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}