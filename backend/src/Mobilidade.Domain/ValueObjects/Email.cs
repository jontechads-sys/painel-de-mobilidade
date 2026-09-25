using Mobilidade.Domain.Excecoes;
using System.Net.Mail;

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

        var normalizado = valor.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalizado, out var email))
        {
            throw new DomainException("Email em formato inválido.");
        }

        return new Email(normalizado);
    }
}