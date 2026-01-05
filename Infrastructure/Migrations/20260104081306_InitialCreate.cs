using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LastName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsBot = table.Column<bool>(type: "boolean", nullable: true),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: true),
                    AddedToAttachmentMenu = table.Column<bool>(type: "boolean", nullable: true),
                    CanJoinGroups = table.Column<bool>(type: "boolean", nullable: true),
                    CanReadAllGroupMessages = table.Column<bool>(type: "boolean", nullable: true),
                    SupportsInlineQueries = table.Column<bool>(type: "boolean", nullable: true),
                    CanConnectToBusiness = table.Column<bool>(type: "boolean", nullable: true),
                    HasPrivateForwards = table.Column<bool>(type: "boolean", nullable: true),
                    HasRestrictedVoiceAndVideoMessages = table.Column<bool>(type: "boolean", nullable: true),
                    IsScam = table.Column<bool>(type: "boolean", nullable: true),
                    IsFake = table.Column<bool>(type: "boolean", nullable: true),
                    TelegramRawJson = table.Column<string>(type: "jsonb", nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
