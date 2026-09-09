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
    [Fact]
    public void Encerrar_QuandoIncidenteJaEstiverEncerrado_LancaInvalidOperationException()
    {
        // Arrange: Cria o incidente e faz o primeiro encerramento
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, "Defesa Civil", "Em analise");
        incidente.Encerrar();

        // Act: Tenta encerrar novamente e captura a exceção correta
        var excecao = Assert.Throws<InvalidOperationException>(() => incidente.Encerrar());

        // Assert: Valida se a mensagem de erro da regra de negócio é a esperada
        Assert.Equal("Incidente já foi encerrado", excecao.Message);
    }
    [Fact]
    public void RegistrarAtendimento_QuandoIncidenteEstaEncerrado_LancaInvalidOperationException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, "Defesa Civil", "Em analise");
        incidente.Encerrar();

        Assert.Throws<InvalidOperationException>(() =>
            incidente.RegistrarAtendimento(DateTime.UtcNow, "Bombeiros", "A caminho"));
    }
    [Fact]
    public void Abrir_SempreCriaComPeloMenosUmAtendimento()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, "Defesa Civil", "Em analise");

        Assert.Single(incidente.Atendimentos);
    }
}