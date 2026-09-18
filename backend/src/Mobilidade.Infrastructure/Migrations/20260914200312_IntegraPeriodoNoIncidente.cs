using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mobilidade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegraPeriodoNoIncidente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
               name: "Periodo_Fim",
               table: "Incidentes",
               type: "datetime2",
               nullable: true);

            migrationBuilder.Sql(
                "UPDATE Incidentes " +
                "SET Periodo_Fim = GETUTCDATE() " +
                "WHERE Encerrado = 1");
                
            migrationBuilder.DropColumn(
                name: "Encerrado",
                table: "Incidentes");

            migrationBuilder.RenameColumn(
                name: "DataHora",
                table: "Incidentes",
                newName: "Periodo_Inicio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Periodo_Fim",
                table: "Incidentes");

            migrationBuilder.RenameColumn(
                name: "Periodo_Inicio",
                table: "Incidentes",
                newName: "DataHora");

            migrationBuilder.AddColumn<bool>(
                name: "Encerrado",
                table: "Incidentes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
