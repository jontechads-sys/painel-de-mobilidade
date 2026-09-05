using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mobilidade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AtendimentoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Atendimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidenteId = table.Column<int>(type: "int", nullable: false),
                    OrgaoResponsavel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Situacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atendimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atendimento_Incidentes_IncidenteId",
                        column: x => x.IncidenteId,
                        principalTable: "Incidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimento_IncidenteId",
                table: "Atendimento",
                column: "IncidenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Atendimento");
        }
    }
}
