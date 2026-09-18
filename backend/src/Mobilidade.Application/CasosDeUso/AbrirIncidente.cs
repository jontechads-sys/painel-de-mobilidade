using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

public sealed record AbrirIncidenteCommand(
    DateTime DataHora,
    Severidade Severidade,
    int BairroId,
    string? Descricao,
    string OrgaoResponsavel,
    string Situacao);

public class AbrirIncidente
{
    private readonly IIncidenteRepository _repositorio;
    private readonly IDespachadorDeEventos _despachador;

    public AbrirIncidente(
        IIncidenteRepository repositorio,
        IDespachadorDeEventos despachador)
    {
        _repositorio = repositorio;
        _despachador = despachador;
    }

    public int Executar(AbrirIncidenteCommand comando)
    {
        var incidente = Incidente.Abrir(
            comando.DataHora,
            comando.Severidade,
            comando.BairroId,
            comando.Descricao,
            comando.OrgaoResponsavel,
            comando.Situacao);

        var incidenteNovo = _repositorio.Criar(incidente);
        incidenteNovo.RegistrarIncidenteAberto();

        foreach (var evento in incidenteNovo.Eventos)
            _despachador.Publicar(evento);

        incidenteNovo.LimparEventos();

        return incidenteNovo.Id;
    }
}