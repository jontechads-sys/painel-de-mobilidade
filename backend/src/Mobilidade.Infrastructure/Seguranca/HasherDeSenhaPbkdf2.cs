using Mobilidade.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Mobilidade.Infrastructure.Seguranca;


public class HasherDeSenhaPbkdf2 : IHasherDeSenha
{
    private static readonly object SemUsuario = new object();
    private readonly PasswordHasher<object> _hasher = new PasswordHasher<object>();

    public string Gerar(string senhaEmTexto)
    {
        return _hasher.HashPassword(SemUsuario, senhaEmTexto);
    }
    public bool Verificar(string senhaEmTexto, string senhaHash)
    {
        var resultado = _hasher.VerifyHashedPassword(SemUsuario, senhaHash, senhaEmTexto);
        return resultado == PasswordVerificationResult.Success
        || resultado == PasswordVerificationResult.SuccessRehashNeeded;
    }
}