using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarketService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Markets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketSeedId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ProgramId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorityPubKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MarketPubKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VaultPubKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VaultAuthorityPubKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CollateralMint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Question = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedTxSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: true),
                    winning_outcome_index = table.Column<byte>(type: "smallint", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    settled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastIndexedSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Markets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<byte>(type: "smallint", nullable: false),
                    State = table.Column<byte>(type: "smallint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    TxSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConfirmedSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AnchorErrorNumber = table.Column<int>(type: "integer", nullable: true),
                    RpcErrorText = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketActions_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketOutcomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeIndex = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketOutcomes_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinningOutcomeIndex = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolveTxSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketResolutions_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMarketPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionPubKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    YesShares = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    NoShares = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Claimed = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMarketPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMarketPositions_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketActions_AttemptCount_CreatedAtUtc",
                table: "MarketActions",
                columns: new[] { "AttemptCount", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketActions_ErrorCode_CreatedAtUtc",
                table: "MarketActions",
                columns: new[] { "ErrorCode", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketActions_IdempotencyKey",
                table: "MarketActions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketActions_MarketId_ActionType_State",
                table: "MarketActions",
                columns: new[] { "MarketId", "ActionType", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketActions_State_CreatedAtUtc",
                table: "MarketActions",
                columns: new[] { "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOutcomes_MarketId_OutcomeIndex",
                table: "MarketOutcomes",
                columns: new[] { "MarketId", "OutcomeIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketResolutions_MarketId",
                table: "MarketResolutions",
                column: "MarketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Markets_AuthorityPubKey_MarketSeedId",
                table: "Markets",
                columns: new[] { "AuthorityPubKey", "MarketSeedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Markets_MarketPubKey",
                table: "Markets",
                column: "MarketPubKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMarketPositions_MarketId",
                table: "UserMarketPositions",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMarketPositions_PositionPubKey",
                table: "UserMarketPositions",
                column: "PositionPubKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMarketPositions_UserId_MarketId",
                table: "UserMarketPositions",
                columns: new[] { "UserId", "MarketId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketActions");

            migrationBuilder.DropTable(
                name: "MarketOutcomes");

            migrationBuilder.DropTable(
                name: "MarketResolutions");

            migrationBuilder.DropTable(
                name: "UserMarketPositions");

            migrationBuilder.DropTable(
                name: "Markets");
        }
    }
}
