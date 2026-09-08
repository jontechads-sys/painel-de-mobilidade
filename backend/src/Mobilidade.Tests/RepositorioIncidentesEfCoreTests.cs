using Mobilidade.Domain.Modelos;
using Mobilidade.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Mobilidade.Infrastructure.Servicos;
using Mobilidade.Infrastructure.Repositorio;

namespace Mobilidade.Tests;

public class RepositorioIncidentesEfCoreTests
{
    [Fact]

    public async Task CriarEBuscarIncidente_ConfirmaQueÉOMesmoIncidente()
    {
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
        .UseSqlServer("Server=localhost,1433;Database=MobilidadeDb;User Id=sa;Password=Mobilidade#2026;TrustServerCertificate=true;")
        .Options;
        var contexto = new IncidentesDbContext(options);
        var incidente = new Incidente(DateTime.UtcNow)
        {
            Id = 0,
            Tipo = TipoIncidente.Colisao,
            Severidade = Severidade.Alta,
            BairroId = 1,
            Descricao = "Colisão de carros na Av Paulista 2006 - SP"
        };
        
        var repositorio = new RepositorioIncidentesEfCore(contexto);

        var incidenteCriado = repositorio.Criar(incidente);
        var resultadoBusca = repositorio.BuscarPorId(incidenteCriado.Id);

        Assert.NotNull(resultadoBusca);
        Assert.Equal(incidente.Descricao ,resultadoBusca.Descricao);
    }

    [Fact]
    public async Task BuscarPorIdInexistente_RetornaNull()
    {
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
        .UseSqlServer("Server=localhost,1433;Database=MobilidadeDb;User Id=sa;Password=Mobilidade#2026;TrustServerCertificate=true;")
        .Options;
        var contexto = new IncidentesDbContext(options);
        var repositorio = new RepositorioIncidentesEfCore(contexto);

       var resultadoDaBusca = repositorio.BuscarPorId(999);
       Assert.Null(resultadoDaBusca);

    }
}