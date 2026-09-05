using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mobilidade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeveridadeObrigatoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Converte incidentes sem severidade (NULL) para NaoTriada (3)
            // antes de tornar a coluna obrigatória.
            migrationBuilder.Sql("UPDATE Incidentes SET Severidade = 3 WHERE Severidade IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "Severidade",
                table: "Incidentes",
                type: "int",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Severidade",
                table: "Incidentes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Reverte NaoTriada (3) de volta para NULL, restaurando o estado original.
            migrationBuilder.Sql("UPDATE Incidentes SET Severidade = NULL WHERE Severidade = 3");
        }
    }
}
