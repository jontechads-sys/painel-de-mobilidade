using Microsoft.EntityFrameworkCore.Migrations;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Seguranca;

#nullable disable

namespace Mobilidade.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeedUsuarioAdmin : Migration
{
    private const string EmailDoAdmin = "admin@mobilidade.local";
    private const string VariavelDeAmbienteDaSenha = "ADMIN_SENHA_INICIAL";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var senha = Environment.GetEnvironmentVariable(VariavelDeAmbienteDaSenha);

        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                $"A variável de ambiente {VariavelDeAmbienteDaSenha} não está definida. " +
                "O seed do usuário Admin inicial precisa dela para gerar o hash da senha.");
        }

        // O hash é calculado aqui, em C#, pelo mesmo hasher que a aplicação usa —
        // o SQL não produz PBKDF2 no formato do PasswordHasher.
        // Usuario.Registrar valida o tamanho mínimo antes de gerar o hash.
        var admin = Usuario.Registrar(
            Email.Criar(EmailDoAdmin),
            senha,
            Papel.Admin,
            new HasherDeSenhaPbkdf2());

        migrationBuilder.InsertData(
            table: "Usuarios",
            columns: new[] { "Email", "SenhaHash", "Papel" },
            values: new object[] { admin.Email.Valor, admin.SenhaHash, admin.Papel.Nome });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "Usuarios",
            keyColumn: "Email",
            keyValue: EmailDoAdmin);
    }
}