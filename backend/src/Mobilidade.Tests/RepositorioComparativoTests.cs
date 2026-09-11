using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

using Mobilidade.Infrastructure.Repositorio;
using Mobilidade.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Xunit;

namespace Mobilidade.Tests;

public class RepositorioComparativoTests
{
    [Fact]
    public void BuscaPorIdInexistente_RetornaNullEmAmbosRepositorios()
    {
        // Arrange
        var repositorioMemoria = new RepositorioIncidentesEmMemoria();
        
        // DbContext in-memory pra EF Core
        var options = new DbContextOptionsBuilder<IncidentesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var contexto = new IncidentesDbContext(options);
        var repositorioEfCore = new RepositorioIncidentesEfCore(contexto);

        // Act
        var resultadoMemoria = repositorioMemoria.BuscarPorId(999);
        var resultadoEfCore = repositorioEfCore.BuscarPorId(999);

        // Assert
        Assert.Null(resultadoMemoria);
        Assert.Null(resultadoEfCore);
    }
}