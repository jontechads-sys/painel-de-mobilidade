using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.CasosDeUso;

public class EncerrarIncidente
{
    private readonly IIncidenteRepository _repositorio;

    public EncerrarIncidente(IIncidenteRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public Incidente Executar(int id)
    {
        var incidente = _repositorio.BuscarPorId(id);
        if (incidente == null)
        {
            throw new KeyNotFoundException("Id inexistente no banco");
        }
        incidente.Encerrar();

        return _repositorio.Salvar(incidente);
    }
}