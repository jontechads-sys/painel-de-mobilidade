using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Seguranca;
using Xunit;

namespace Mobilidade.Tests;

public class UsuarioTests
{
    [Fact]
    public void SenhaConfere_ComSenhaCerta_RetornaTrue()
    {
        // Arrange
        var hasher = new HasherDeSenhaPbkdf2();
        var email = Email.Criar("fulano@teste.com");
        var usuario = Usuario.Registrar(email, "senha12345", Papel.Operador, hasher);

        // Act
        var confere = usuario.SenhaConfere("senha12345", hasher);

        // Assert
        Assert.True(confere);
    }
    [Fact]
    public void SenhaConfere_ComSenhaErrada_RetornaFalse()
    {
        // Arrange
        var hasher = new HasherDeSenhaPbkdf2();
        var email = Email.Criar("fulano@teste.com");
        var usuario = Usuario.Registrar(email, "senha12345", Papel.Operador, hasher);

        // Act
        var confere = usuario.SenhaConfere("senha54321", hasher);

        // Assert
        Assert.False(confere);
    }
}