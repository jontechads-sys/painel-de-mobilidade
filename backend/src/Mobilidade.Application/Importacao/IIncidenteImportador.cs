using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

namespace Mobilidade.Application.Importacao;

public sealed record ResultadoImportacao(
    IReadOnlyList<Incidente> Incidentes,
    IReadOnlyList<string> Avisos);

public interface IIncidenteImportador
{
    Task<ResultadoImportacao> ImportarAsync(CancellationToken ct = default);
}