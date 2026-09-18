using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mobilidade.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AtendimentoSinteticoParaIncidentesLegados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO Atendimento (IncidenteId, OrgaoResponsavel, DataHora, Situacao)
                SELECT i.Id,
                       'Importacao (dado legado)',
                       i.Periodo_Inicio,
                       'Sem registro de atendimento'
                FROM Incidentes i
                WHERE NOT EXISTS (SELECT 1 FROM Atendimento a WHERE a.IncidenteId = i.Id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM Atendimento
                WHERE OrgaoResponsavel = 'Importacao (dado legado)'
                  AND Situacao = 'Sem registro de atendimento'");
        }
    }
}