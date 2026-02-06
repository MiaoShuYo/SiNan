using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiNan.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Resource = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", maxLength: 65535, nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", maxLength: 65535, nullable: true),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "config_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Namespace = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 65535, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PublishedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Namespace = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "config_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 65535, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PublishedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_config_history_config_items_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "config_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstanceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 100),
                    Healthy = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    MetadataJson = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TtlSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    IsEphemeral = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_instances_services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_config_history_ConfigId_Version",
                table: "config_history",
                columns: new[] { "ConfigId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_config_items_Namespace_Group_Key",
                table: "config_items",
                columns: new[] { "Namespace", "Group", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_instances_ServiceId",
                table: "service_instances",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_service_instances_ServiceId_Host_Port",
                table: "service_instances",
                columns: new[] { "ServiceId", "Host", "Port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_services_Namespace_Group_Name",
                table: "services",
                columns: new[] { "Namespace", "Group", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "config_history");

            migrationBuilder.DropTable(
                name: "service_instances");

            migrationBuilder.DropTable(
                name: "config_items");

            migrationBuilder.DropTable(
                name: "services");
        }
    }
}
