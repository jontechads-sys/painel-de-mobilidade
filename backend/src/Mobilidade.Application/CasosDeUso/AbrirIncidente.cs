using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

public class AbrirIncidente
{
    private readonly IIncidenteRepository _repositorio;

    public AbrirIncidente(IIncidenteRepository repositorio)
    {
        _repositorio = repositorio;

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
        return incidenteNovo.Id;
    }
}