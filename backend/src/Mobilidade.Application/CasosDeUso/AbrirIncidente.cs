using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Application.CasosDeUso;

public class AbrirIncidente
{
    private readonly IIncidenteRepository _repositorio;

    public AbrirIncidente(IIncidenteRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public Incidente Executar(
        DateTime dataHora,
        Severidade severidade,
        int bairroId,
        string? descricao,
        string orgaoResponsavel,
        string situacao)
    {
        var incidente = Incidente.Abrir(dataHora, severidade, bairroId, descricao, orgaoResponsavel, situacao);
        return _repositorio.Criar(incidente);
    }
}