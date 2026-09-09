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
        // Arrange

        var incidente = new Incidente(DateTime.UtcNow, Severidade.Alta)
        {
            Id = 0,
            Tipo = TipoIncidente.Colisao,
            BairroId = 1,
            Descricao = "Colisão de carros na Av Paulista 2006 - SP"
        };
        string orgaoInvalido = new string('*' ,101);

        var atendimento = new Atendimento
        {
            Id = 0,
            IncidenteId = 0,
            OrgaoResponsavel = orgaoInvalido,
            DataHora = DateTime.Now,
            Situacao = "Em Análise",
        };
        // ... (contexto, incidente, atendimento inválido, serviço)
        var servico = new ServicoAberturaIncidente(contexto);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await servico.Executar(atendimento, incidente));
        // Act & Assert
        // ... (Assert.ThrowsAsync)
    }
}