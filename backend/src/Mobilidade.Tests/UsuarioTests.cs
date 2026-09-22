using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Seguranca;
using Mobilidade.Domain.Excecoes;
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
    [Fact]
    public void Registrar_MesmaSenhaEmDoisUsuarios_GeraHashesDiferentes()
    {
        var hasher = new HasherDeSenhaPbkdf2();


        var emailUsuario1 = Email.Criar("usuario1@mobilidade.com");
        var usuario1 = Usuario.Registrar(emailUsuario1, "mobilidade12345", Papel.Admin, hasher);

        var emailUsuario2 = Email.Criar("usuario2@mobilidade.com");
        var usuario2 = Usuario.Registrar(emailUsuario2, "mobilidade12345", Papel.Admin, hasher);

        Assert.NotEqual(usuario1.SenhaHash, usuario2.SenhaHash);
    }
    [Fact]
    public void Registrar_ComSenhaCurta_LancaDomainException()
    {
        var hasher = new HasherDeSenhaPbkdf2();
        var email = Email.Criar("fulano@teste.com");

        Assert.Throws<DomainException>(() => Usuario.Registrar(email, "123456", Papel.Operador, hasher));
    }
}