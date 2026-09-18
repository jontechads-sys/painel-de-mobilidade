using Mobilidade.Application.CasosDeUso;
using Mobilidade.Application.Eventos.Manipuladores;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Repositorio;
using Xunit;

namespace Mobilidade.Tests;

public class AuditoriaTests
{
    private static (AbrirIncidente casoDeUso, RepositorioAuditoriaEmMemoria auditoria) Montar()
    {
        var repositorioIncidentes = new RepositorioIncidentesEmMemoria();
        var repositorioAuditoria = new RepositorioAuditoriaEmMemoria();
        var manipulador = new RegistrarAuditoriaAoAbrirIncidenteGrave(repositorioAuditoria);
        var despachador = new DespachadorComManipuladores(manipulador);

        return (new AbrirIncidente(repositorioIncidentes, despachador), repositorioAuditoria);
    }

    [Fact]
    public void Executar_ComSeveridadeAlta_GeraUmRegistroDeAuditoria()
    {
        var (casoDeUso, auditoria) = Montar();

        casoDeUso.Executar(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");

        Assert.Single(auditoria.Registros);
    }

    [Fact]
    public void Executar_ComSeveridadeBaixa_NaoGeraRegistroDeAuditoria()
    {
        var (casoDeUso, auditoria) = Montar();

        casoDeUso.Executar(DateTime.UtcNow, Severidade.Baixa, 1, "teste", "Defesa Civil", "Em analise");

        Assert.Empty(auditoria.Registros);
    }
}