using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Eventos;
using Mobilidade.Infrastructure.Repositorio;

using Xunit;
using Mobilidade.Application.CasosDeUso;

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
    [Fact]
    public void Abrir_DeveTerPeriodoComInicioIgualDataHora()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow.AddHours(-1), Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        Assert.NotNull(incidente.Periodo);
    }
    [Fact]
    public void Encerrar_QuandoJaEstaEncerrado_NaoAdicionaSegundoEventoNaLista()
    {
        // Arrange
        // TODO: cria um Incidente válido via Incidente.Abrir(...)
        // TODO: chama Encerrar() uma vez (deve funcionar, gera 1 evento)
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise");
        incidente.Encerrar();
        // Act
        // TODO: chama Encerrar() de novo — isso deve lançar exceção
        Assert.Throws<InvalidOperationException>(() => incidente.Encerrar());
        // Assert 
        // TODO: verifica que a lista de eventos continua com só 1 item
        //       (não 2 — a segunda tentativa não deve ter adicionado nada)
        Assert.Single(incidente.Eventos);
    }

    [Fact]
    public void Encerrar_IncidenteAberto_AdicionaEventoNaLista()
    {
        // Arrange
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise");
        // Act
        incidente.Encerrar();
        // Assert
        // TODO: verifica que a lista de eventos tem exatamente 1 item
        // TODO: verifica que esse item é do tipo IncidenteEncerrado
        Assert.Single(incidente.Eventos);
        Assert.IsType<IncidenteEncerrado>(incidente.Eventos.First());
    }
    [Fact]
    public void Executar_IncidenteValido_DespachaIncidenteAbertoComIdReal()
    {
        var repositorio = new RepositorioIncidentesEmMemoria();
        var despachador = new DespachadorEmMemoria();
        var abrirIncidente = new AbrirIncidente(repositorio, despachador);

        var id = abrirIncidente.Executar(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise");

        var evento = Assert.IsType<IncidenteAberto>(Assert.Single(despachador.Publicados));
        Assert.Equal(id, evento.IncidenteId);
    }

}