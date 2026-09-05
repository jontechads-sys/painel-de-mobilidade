using System.ComponentModel.DataAnnotations;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Api.Dtos;

public sealed record CriarIncidenteRequest(
    DateTime DataHora,
    TipoIncidente Tipo,
    Severidade Severidade,
    int BairroId,
    //[Required, MinLength(1)]string Bairro,
    string? Descricao
    );