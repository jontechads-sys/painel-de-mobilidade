using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Interfaces;
using Mobilidade.Domain.Excecoes;

namespace Mobilidade.Domain.Entity;

public class Usuario
{
    public int Id { get; private set; }
    public Email Email { get; private set; }
    public string SenhaHash { get; private set; }
    public Papel Papel { get; private set; }

    private Usuario() { }

    public static Usuario Registrar(Email email, string senhaEmTexto, Papel papel, IHasherDeSenha hasher)
    {
        if (string.IsNullOrWhiteSpace(senhaEmTexto) || senhaEmTexto.Length < 8)
        {
            throw new DomainException("Senha precisa ter pelo menos 8 caracteres");
        }

        return new Usuario
        {
            Email = email,
            Papel = papel,
            SenhaHash = hasher.Gerar(senhaEmTexto)
        };
    }   

    public bool SenhaConfere(string senhaEmTexto, IHasherDeSenha hasher)
    {
        return hasher.Verificar(senhaEmTexto, SenhaHash);
    }
}