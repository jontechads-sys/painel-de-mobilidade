using Mobilidade.Domain.Excecoes;
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
    public void Criar_ComDataHoraFutura_LancaDomainException()
    {
        var dataFutura = DateTime.UtcNow.AddDays(1);

        Assert.Throws<DomainException>(() =>
            Incidente.Abrir(dataFutura, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise"));
    }

    [Fact]
    public void Criar_ComSeveridadeNaoTriada_LancaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            Incidente.Abrir(DateTime.UtcNow, Severidade.NaoTriada, 1, "teste", "Defesa Civil", "Em analise"));
    }
    [Fact]
    public void Encerrar_QuandoIncidenteJaEstiverEncerrado_LancaDomainException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        incidente.Encerrar();

        var excecao = Assert.Throws<DomainException>(() => incidente.Encerrar());

        Assert.Equal("Incidente já foi encerrado", excecao.Message);
    }

    [Fact]
    public void RegistrarAtendimento_QuandoIncidenteEstaEncerrado_LancaDomainException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        incidente.Encerrar();

        Assert.Throws<DomainException>(() =>
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
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise");
        incidente.Encerrar();

        Assert.Throws<DomainException>(() => incidente.Encerrar());
        Assert.Single(incidente.Eventos);
    }

    [Fact]
    public void Encerrar_IncidenteAberto_AdicionaEventoNaLista()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise");
        incidente.Encerrar();

        Assert.Single(incidente.Eventos);
        Assert.IsType<IncidenteEncerrado>(incidente.Eventos.First());
    }
    [Fact]
    public void Executar_IncidenteValido_DespachaIncidenteAbertoComIdReal()
    {
        var repositorio = new RepositorioIncidentesEmMemoria();
        var despachador = new DespachadorEmMemoria();
        var abrirIncidente = new AbrirIncidente(repositorio, despachador);

        var id = abrirIncidente.Executar(new AbrirIncidenteCommand(
          DateTime.UtcNow, Severidade.Alta, 1, "teste", "Serviço Militar", "Em analise"));

        var evento = Assert.IsType<IncidenteAberto>(Assert.Single(despachador.Publicados));
        Assert.Equal(id, evento.IncidenteId);
    }
    [Fact]
    public void MudarBairro_IncidenteAberto_AlteraOBairro()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");

        incidente.MudarBairro(7);

        Assert.Equal(7, incidente.BairroId);
    }

    [Fact]
    public void MudarBairro_IncidenteEncerrado_LancaDomainException()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");
        incidente.Encerrar();

        var excecao = Assert.Throws<DomainException>(() => incidente.MudarBairro(7));

        Assert.Equal("Incidente encerrado não pode ter o bairro alterado.", excecao.Message);
    }
    [Fact]
    public void RegistrarIncidenteAberto_DeveLancarExcecao_QuandoIdForZero()
    {
        var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise"); 

        Assert.Equal(0, incidente.Id);
        Assert.Throws<DomainException>(() => incidente.RegistrarIncidenteAberto());
    }

}