using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Entity;

namespace Mobilidade.Application.CasosDeUso;

public class RegistrarAtendimento
{
    private readonly IIncidenteRepository _repositorio;

    public RegistrarAtendimento(IIncidenteRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public int Executar(
        int id,
        DateTime dataHora,
        string orgaoReponsavel,
        string situacao)
    {
        var incidente = _repositorio.BuscarPorId(id);
        if (incidente == null)
        {
            throw new KeyNotFoundException("Id inexistente no banco");
        }
        incidente.RegistrarAtendimento(dataHora, orgaoReponsavel, situacao);
        _repositorio.Salvar(incidente);
        return incidente.Id;
    }
}