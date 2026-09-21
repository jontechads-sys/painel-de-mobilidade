using Mobilidade.Domain.Excecoes;

namespace Mobilidade.Domain.ValueObjects;

public class Email
{
    public string Valor { get; private set; }

    private Email(string valor)
    {
        Valor = valor;
    }

    public static Email Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException("Email é obrigatório.");
        }
        return new Email(valor);
    }
}