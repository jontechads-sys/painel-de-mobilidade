using System.ComponentModel.DataAnnotations;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;


namespace Mobilidade.Api.Dtos;

public sealed record AtualizarIncidenteRequest(
    TipoIncidente Tipo,
    int BairroId,
    //[Required, MinLength(1)] string Bairro,
    string? Descricao);