using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowFramework.Dashboard.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowHistoryGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryNodes",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ExecutionCount = table.Column<long>(type: "INTEGER", nullable: false),
                    SuccessCount = table.Column<long>(type: "INTEGER", nullable: false),
                    FailureCount = table.Column<long>(type: "INTEGER", nullable: false),
                    AverageDurationTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    WorkflowNamesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryNodes", x => x.Fingerprint);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshTokenExpiry = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEdges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TargetFingerprint = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Weight = table.Column<long>(type: "INTEGER", nullable: false),
                    AverageTransitionTimeTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    WorkflowNamesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEdges_HistoryNodes_SourceFingerprint",
                        column: x => x.SourceFingerprint,
                        principalTable: "HistoryNodes",
                        principalColumn: "Fingerprint",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoryEdges_HistoryNodes_TargetFingerprint",
                        column: x => x.TargetFingerprint,
                        principalTable: "HistoryNodes",
                        principalColumn: "Fingerprint",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", nullable: false),
                    ScopesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    StepResultsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", nullable: true),
                    Author = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowVersions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StepRuns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    StepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepRuns_WorkflowRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyPrefix",
                table: "ApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action",
                table: "AuditEntries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Timestamp",
                table: "AuditEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_WorkflowId",
                table: "AuditEntries",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEdges_SourceFingerprint_TargetFingerprint_Kind",
                table: "HistoryEdges",
                columns: new[] { "SourceFingerprint", "TargetFingerprint", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEdges_TargetFingerprint",
                table: "HistoryEdges",
                column: "TargetFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryNodes_LastSeenAt",
                table: "HistoryNodes",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryNodes_Name",
                table: "HistoryNodes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_RunId",
                table: "StepRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId_Key",
                table: "UserSettings",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_StartedAt",
                table: "WorkflowRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_Status",
                table: "WorkflowRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_UserId",
                table: "WorkflowRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                table: "WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_IsDeleted",
                table: "Workflows",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Name",
                table: "Workflows",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_OwnerId",
                table: "Workflows",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersions_WorkflowId_VersionNumber",
                table: "WorkflowVersions",
                columns: new[] { "WorkflowId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "HistoryEdges");

            migrationBuilder.DropTable(
                name: "StepRuns");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "WorkflowVersions");

            migrationBuilder.DropTable(
                name: "HistoryNodes");

            migrationBuilder.DropTable(
                name: "WorkflowRuns");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
