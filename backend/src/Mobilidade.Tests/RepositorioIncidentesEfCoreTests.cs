using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

using Mobilidade.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Mobilidade.Infrastructure.Repositorio;

namespace Mobilidade.Tests;

public class RepositorioIncidentesEfCoreTests
{
    [Fact]
    public async Task CriarEBuscarIncidente_ConfirmaQueÉOMesmoIncidente()
    {
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
        .UseSqlServer(TestesConfig.ConnectionString)
        .Options;
        var contexto = new IncidentesDbContext(options);
        var incidente = Incidente.Abrir(
            DateTime.UtcNow, 
            Severidade.Alta, 
            bairroId: 1,
            descricao: "Colisão de carros na Av Paulista 2006 - SP", 
            orgaoResponsavel: "Defesa Civil", 
            situacao: "Em analise");
        incidente.Id = 0;
        incidente.Tipo = TipoIncidente.Colisao;

        var repositorio = new RepositorioIncidentesEfCore(contexto);

        var incidenteCriado = repositorio.Criar(incidente);
        var resultadoBusca = repositorio.BuscarPorId(incidenteCriado.Id);

        Assert.NotNull(resultadoBusca);
        Assert.Equal(incidente.Descricao, resultadoBusca.Descricao);
    }

    [Fact]
    public async Task BuscarPorIdInexistente_RetornaNull()
    {
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
        .UseSqlServer(TestesConfig.ConnectionString)
        .Options;
        var contexto = new IncidentesDbContext(options);
        var repositorio = new RepositorioIncidentesEfCore(contexto);

        var resultadoDaBusca = repositorio.BuscarPorId(999);
        Assert.Null(resultadoDaBusca);

    }
}