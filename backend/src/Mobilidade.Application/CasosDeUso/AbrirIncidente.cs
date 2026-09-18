using Mobilidade.Application.Eventos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

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

    public int Executar(
        DateTime dataHora,
        Severidade severidade,
        int bairroId,
        string? descricao,
        string orgaoResponsavel,
        string situacao)
    {
        var incidente = Incidente.Abrir(dataHora, severidade, bairroId, descricao, orgaoResponsavel, situacao);

        var incidenteNovo = _repositorio.Criar(incidente);
        incidenteNovo.RegistrarIncidenteAberto();

        foreach (var evento in incidenteNovo.Eventos)
            _despachador.Publicar(evento);

        incidenteNovo.LimparEventos();

        return incidenteNovo.Id;
    }
}