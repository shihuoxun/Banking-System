using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingSystem.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNameAndLastLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Accounts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Accounts");
        }
    }
}
