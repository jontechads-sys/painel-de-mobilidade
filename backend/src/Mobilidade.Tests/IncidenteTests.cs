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
        Assert.Throws<ArgumentException>(() => new Incidente(dataFutura, Severidade.Alta));
    }

    [Fact]
    public void Criar_ComSeveridadeNaoTriada_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Incidente(DateTime.UtcNow, Severidade.NaoTriada));
    }

    [Fact]
    public void Criar_ComSeveridadeForaDoEnum_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Incidente(DateTime.UtcNow, (Severidade)99));
    }
}