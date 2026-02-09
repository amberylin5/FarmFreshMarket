using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmFreshMarket.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLoginTimeToMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinimumPasswordAgeDays",
                table: "PasswordPolicies",
                newName: "MinimumPasswordAgeMinutes");

            migrationBuilder.RenameColumn(
                name: "MaximumPasswordAgeDays",
                table: "PasswordPolicies",
                newName: "MaximumPasswordAgeMinutes");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginTime",
                table: "Members",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginTime",
                table: "Members");

            migrationBuilder.RenameColumn(
                name: "MinimumPasswordAgeMinutes",
                table: "PasswordPolicies",
                newName: "MinimumPasswordAgeDays");

            migrationBuilder.RenameColumn(
                name: "MaximumPasswordAgeMinutes",
                table: "PasswordPolicies",
                newName: "MaximumPasswordAgeDays");
        }
    }
}
