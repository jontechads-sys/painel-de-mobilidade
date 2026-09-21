using Mobilidade.Domain.Excecoes;

namespace Mobilidade.Domain.ValueObjects;

public class Papel
{
    public string Nome { get; private set; }

    private Papel(string nome)
    {
        Nome = nome;
    }
    public static readonly Papel Admin = new Papel("Admin");
    public static readonly Papel Operador = new Papel("Operador");

    public static Papel DeNome(string nome)
    {
        if(nome == "Admin")
        {
            return Admin;
        }
        if(nome == "Operador")
        {
            return Operador;
        }
        throw new DomainException("Usuario Inválido");
    }
}