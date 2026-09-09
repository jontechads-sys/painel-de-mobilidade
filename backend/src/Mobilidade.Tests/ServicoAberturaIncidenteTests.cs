using Mobilidade.Domain.Modelos;
using Mobilidade.Infrastructure.Data;
using Mobilidade.Infrastructure.Servicos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mobilidade.Tests;

public class ServicoAberturaIncidenteTests
{
    [Fact]
    public async Task Executar_ComAtendimentoInvalido_DesfazIncidenteViaRollback()
    {
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
        .UseSqlServer("Server=localhost,1433;Database=MobilidadeDb;User Id=sa;Password=Mobilidade#2026;TrustServerCertificate=true;")
        .Options;

        var contexto = new IncidentesDbContext(options);
        var servico = new ServicoAberturaIncidente(contexto);

        string orgaoInvalido = new string('*', 101);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await servico.Executar(DateTime.UtcNow, Severidade.Alta, orgaoInvalido, "Em Analise"));
    }
}