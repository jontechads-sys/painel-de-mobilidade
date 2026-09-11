using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Infrastructure.Repositorio;

public class RepositorioIncidentesEmMemoria : IIncidenteRepository
{
    private readonly Dictionary<int, Incidente> _incidentes;
    private int _proximoId = 1;

    public RepositorioIncidentesEmMemoria()
    {
        _incidentes = new Dictionary<int, Incidente>();
    }
    public Incidente Criar(Incidente incidente)
    {
        incidente.Id = _proximoId;
        _incidentes[incidente.Id] = incidente;
        _proximoId++;
        return incidente;
    }
    public IReadOnlyList<Incidente> Listar()
    {
        return _incidentes.Values.ToList();
    }
    public Incidente? BuscarPorId(int id)
    {
        return _incidentes.TryGetValue(id, out var incidente) ? incidente : null;
    }
    public Incidente? Atualizar(int id, Incidente incidente)
    {
        if (!_incidentes.TryGetValue(id, out var existente))
        {
            return null;
        }
        existente.Tipo = incidente.Tipo;
        existente.BairroId = incidente.BairroId;
        existente.Descricao = incidente.Descricao;

        return existente;
    }
    public bool Remover(int id)
    {
        return _incidentes.Remove(id);
    }
    public Incidente Salvar(Incidente incidente)
    {
        _incidentes[incidente.Id] = incidente;
        return incidente;
    }
}