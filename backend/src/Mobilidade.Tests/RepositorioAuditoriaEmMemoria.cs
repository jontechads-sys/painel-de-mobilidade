using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;

namespace Mobilidade.Tests;

public sealed class RepositorioAuditoriaEmMemoria : IAuditoriaRepository
{
    public List<Auditoria> Registros { get; } = new();

    public Auditoria Criar(Auditoria auditoria)
    {
        Registros.Add(auditoria);
        return auditoria;
    }
}