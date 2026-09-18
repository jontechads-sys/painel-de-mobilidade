using System.ComponentModel.DataAnnotations;
using Mobilidade.Domain.Entity;

namespace Mobilidade.Api.Dtos;

public sealed record CriarIncidenteRequest(
    [Required(ErrorMessage = "DataHora é obrigatória.")]
    DateTime? DataHora,

    [Required(ErrorMessage = "Tipo é obrigatório.")]
    [EnumDataType(typeof(TipoIncidente), ErrorMessage = "Tipo inválido.")]
    TipoIncidente? Tipo,

    [Required(ErrorMessage = "Severidade é obrigatória.")]
    [Range(0, 3, ErrorMessage = "Severidade deve estar entre 0 e 3.")]
    int? Severidade,

    [Required(ErrorMessage = "BairroId é obrigatório.")]
    [Range(1, int.MaxValue, ErrorMessage = "BairroId deve ser maior que zero.")]
    int? BairroId,

    [MaxLength(500, ErrorMessage = "Descricao deve ter no máximo 500 caracteres.")]
    string? Descricao,

    [Required(ErrorMessage = "OrgaoResponsavel é obrigatório.")]
    [MaxLength(100, ErrorMessage = "OrgaoResponsavel deve ter no máximo 100 caracteres.")]
    string? OrgaoResponsavel,

    [Required(ErrorMessage = "Situacao é obrigatória.")]
    [MaxLength(100, ErrorMessage = "Situacao deve ter no máximo 100 caracteres.")]
    string? Situacao
);