using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;
using Mobilidade.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mobilidade.Infrastructure.Repositorio;

public class RepositorioIncidentesEfCore : IIncidenteRepository
{
    private readonly IncidentesDbContext _contexto;

    public RepositorioIncidentesEfCore(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public Incidente Criar(Incidente incidente)
    {
        _contexto.Incidentes.Add(incidente);
        _contexto.SaveChanges();
        return incidente;
    }

    public IReadOnlyList<Incidente> Listar()
    {
        return _contexto.Incidentes.ToList();
    }

    public Incidente? BuscarPorId(int id)
    {
        return _contexto.Incidentes.FirstOrDefault(i => i.Id == id);
    }

    public Incidente? Atualizar(int id, Incidente incidente)
    {
        var existente = _contexto.Incidentes.FirstOrDefault(i => i.Id == id);
        if (existente == null)
            return null;

        existente.Tipo = incidente.Tipo;
        existente.BairroId = incidente.BairroId;
        existente.Descricao = incidente.Descricao;

        _contexto.SaveChanges();
        return existente;
    }

    public bool Remover(int id)
    {
        var incidente = _contexto.Incidentes.FirstOrDefault(i => i.Id == id);
        if (incidente == null)
            return false;

        _contexto.Incidentes.Remove(incidente);
        _contexto.SaveChanges();
        return true;
    }
    public Incidente Salvar(Incidente incidente)
    {
        _contexto.SaveChanges();
        return incidente;
    }
}