using Microsoft.EntityFrameworkCore;
using Mobilidade.Application.Consultas;
using Mobilidade.Infrastructure.Data;

namespace Mobilidade.Infrastructure.Consultas;

public sealed record BuscarIncidentePorIdQuery(int Id);

public sealed class BuscarIncidentePorIdHandler
{
    private readonly IncidentesDbContext _contexto;

    public BuscarIncidentePorIdHandler(IncidentesDbContext contexto)
    {
        _contexto = contexto;
    }

    public IncidenteDetalheDto? Handle(BuscarIncidentePorIdQuery query)
        => _contexto.Incidentes
            .AsNoTracking()
            .Where(i => i.Id == query.Id)
            .Select(i => new IncidenteDetalheDto(
                i.Id,
                i.Periodo.Inicio,
                i.Periodo.Fim,
                i.Severidade.Nome,
                i.Bairro!.Nome,
                i.Tipo,
                i.Descricao,
                i.Atendimentos.Count))
            .FirstOrDefault();
}