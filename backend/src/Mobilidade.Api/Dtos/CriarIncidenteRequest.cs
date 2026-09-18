using System.ComponentModel.DataAnnotations;
using Mobilidade.Domain.Entity;


namespace Mobilidade.Api.Dtos;

public sealed record CriarIncidenteRequest(
    DateTime DataHora,
    TipoIncidente Tipo,
    int Severidade,
    int BairroId,
    string? Descricao,
    string OrgaoResponsavel,
    string Situacao
);