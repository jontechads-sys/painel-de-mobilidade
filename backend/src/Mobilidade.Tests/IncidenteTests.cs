using Mobilidade.Domain.Modelos;
using Xunit;

namespace Mobilidade.Tests;

public class IncidenteTests
{
    [Fact]
    public void Criar_ComDataHoraFutura_LancaArgumentException()
    {
        // Arrange: uma data no futuro
        var dataFutura = DateTime.UtcNow.AddDays(1);

        // Act & Assert: criar o incidente com essa data deve lançar
        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(dataFutura, Severidade.Alta, "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Criar_ComSeveridadeNaoTriada_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(DateTime.UtcNow, Severidade.NaoTriada, "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Criar_ComSeveridadeForaDoEnum_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(DateTime.UtcNow, (Severidade)99, "Defesa Civil", "Em analise"));
    }
}