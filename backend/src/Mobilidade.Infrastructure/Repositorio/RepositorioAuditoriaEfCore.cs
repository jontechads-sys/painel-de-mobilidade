using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Repositorio;

public class RepositorioAuditoriaEfCore : IAuditoriaRepository
{
    private readonly IncidentesDbContext _contexto;

    public RepositorioAuditoriaEfCore(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public Auditoria Criar(Auditoria auditoria)
    {
        _contexto.Auditorias.Add(auditoria);
        _contexto.SaveChanges();
        return auditoria;
    }
}