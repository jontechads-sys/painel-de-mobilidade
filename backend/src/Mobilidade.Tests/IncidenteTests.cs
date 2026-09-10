using Mobilidade.Domain.Modelos;
using Xunit;

namespace Mobilidade.Tests;

public class IncidenteTests
{
    [Fact]
    public void Criar_ComDataHoraFutura_LancaArgumentException()
    {
        var dataFutura = DateTime.UtcNow.AddDays(1);

        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(dataFutura, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Criar_ComSeveridadeNaoTriada_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(DateTime.UtcNow, Severidade.NaoTriada, 1, "teste", "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Criar_ComSeveridadeForaDoEnum_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Incidente.Abrir(DateTime.UtcNow, (Severidade)99, 1, "teste", "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Encerrar_QuandoIncidenteJaEstiverEncerrado_LancaInvalidOperationException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        incidente.Encerrar();

        var excecao = Assert.Throws<InvalidOperationException>(() => incidente.Encerrar());

        Assert.Equal("Incidente já foi encerrado", excecao.Message);
    }

    [Fact]
    public void RegistrarAtendimento_QuandoIncidenteEstaEncerrado_LancaInvalidOperationException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        incidente.Encerrar();

        Assert.Throws<InvalidOperationException>(() =>
            incidente.RegistrarAtendimento(DateTime.UtcNow, "Bombeiros", "A caminho"));
    }

    [Fact]
    public void Abrir_SempreCriaComPeloMenosUmAtendimento()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");

        Assert.Single(incidente.Atendimentos);
    }
}