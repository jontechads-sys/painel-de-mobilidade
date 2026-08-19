using Mobilidade.Domain.Modelos;

namespace Mobilidade.Api.Dtos;

public sealed record AtualizarIncidenteRequest(
    DateTime DataHora,
    TipoIncidente Tipo,
    Severidade? Severidade,
    string Bairro,
    string? Descricao);