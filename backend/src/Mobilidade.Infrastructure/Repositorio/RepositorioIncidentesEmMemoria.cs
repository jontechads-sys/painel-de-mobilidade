using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;

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
        var incidenteFinal = incidente with { Id = _proximoId };
        _incidentes[incidenteFinal.Id] = incidenteFinal;
        _proximoId++;
        return incidenteFinal;
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
        if (!_incidentes.ContainsKey(id))
        {
            return null;
        }
        var incidenteAtualizado = incidente with {Id = id};
        _incidentes[id] = incidenteAtualizado;
        return incidenteAtualizado;
    }
    public bool Remover(int id)
    {
        return _incidentes.Remove(id);
    }

}