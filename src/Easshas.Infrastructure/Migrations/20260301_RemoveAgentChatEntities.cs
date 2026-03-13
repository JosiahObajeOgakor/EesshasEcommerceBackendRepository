using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Easshas.Infrastructure.Migrations
{
    public partial class RemoveAgentChatEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS to avoid errors when the tables are already absent
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AgentChatMessages\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AgentChatSessions\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Context = table.Column<string>(maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentChatSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(maxLength: 2000, nullable: false),
                    AgentResponse = table.Column<string>(maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentChatMessages_AgentChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AgentChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentChatMessages_SessionId",
                table: "AgentChatMessages",
                column: "SessionId");
        }
    }
}
