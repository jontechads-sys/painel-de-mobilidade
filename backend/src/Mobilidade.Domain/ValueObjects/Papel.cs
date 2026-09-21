
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
}