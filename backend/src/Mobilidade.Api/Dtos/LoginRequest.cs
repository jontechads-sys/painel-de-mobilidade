using System.ComponentModel.DataAnnotations;

namespace Mobilidade.Api.Dtos;

public sealed record LoginRequest(
    [Required(ErrorMessage = "Email é obrigatório.")]
    string Email,
    [Required(ErrorMessage = "Senha é obrigatória.")]
    string Senha);