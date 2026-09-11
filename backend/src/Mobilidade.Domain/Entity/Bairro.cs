namespace Mobilidade.Domain.Entity;

public sealed class Bairro
{
    public int Id { get; private set; }
    public string Nome { get; private set; }

    public Bairro(int id, string nome)
    {
        Id = id;
        Nome = nome;
    }
}