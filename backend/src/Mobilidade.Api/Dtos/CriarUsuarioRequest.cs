using System.ComponentModel.DataAnnotations;

namespace Mobilidade.Api.Dtos;

public sealed record CriarUsuarioRequest(
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email em formato inválido.")]
    [StringLength(254, ErrorMessage = "Email não pode ter mais de 254 caracteres.")]
    string Email,

    [Required(ErrorMessage = "Senha é obrigatória.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Senha precisa ter pelo menos 8 caracteres.")]
    string Senha,

    [Required(ErrorMessage = "Papel é obrigatório.")]
    string Papel
);