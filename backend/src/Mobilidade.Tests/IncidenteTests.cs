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
}