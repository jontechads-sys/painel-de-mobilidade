using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Infrastructure.Seguranca;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Mobilidade.Tests.Integracao;

public class ApiEmMemoriaFactory : WebApplicationFactory<Program>
{
    public const string Chave = "chave-de-teste-com-no-minimo-32-caracteres!!";
    public const string Emissor = "painel-mobilidade";
    public const string Audiencia = "painel-mobilidade";

    public ApiEmMemoriaFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Chave", Chave);
        Environment.SetEnvironmentVariable("Jwt__Emissor", Emissor);
        Environment.SetEnvironmentVariable("Jwt__Audiencia", Audiencia);
    }

    public static string TokenDeOperador()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Chave"] = Chave,
                ["Jwt:Emissor"] = Emissor,
                ["Jwt:Audiencia"] = Audiencia,
            })
            .Build();

        var usuario = Usuario.Registrar(
            Email.Criar("operador@teste.com"), "senha-de-teste-123",
            Papel.Operador, new HasherDeSenhaPbkdf2());

        return new GeradorDeTokenJwt(config).Gerar(usuario);
    }

    public static string GerarToken(string chave, string role, DateTime expiraUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Emissor,
            audience: Audiencia,
            claims: new[] { new Claim("role", role) },
            expires: expiraUtc,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class AutorizacaoIntegracaoTests : IClassFixture<ApiEmMemoriaFactory>
{
    private readonly ApiEmMemoriaFactory _factory;

    public AutorizacaoIntegracaoTests(ApiEmMemoriaFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/Incidentes")]
    [InlineData("GET", "/api/Incidentes/2")]
    [InlineData("GET", "/api/Incidentes/bairro/1?inicio=2026-09-01T00:00:00")]
    [InlineData("POST", "/api/Incidentes")]
    [InlineData("DELETE", "/api/Incidentes/999")]
    [InlineData("POST", "/api/Usuarios")]
    public async Task SemToken_Retorna401(string metodo, string rota)
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(new HttpMethod(metodo), rota);
        if (metodo == "POST")
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/Incidentes")]
    [InlineData("DELETE", "/api/Incidentes/999")]
    [InlineData("POST", "/api/Usuarios")]
    public async Task OperadorEmEscrita_Retorna403(string metodo, string rota)
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(new HttpMethod(metodo), rota);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiEmMemoriaFactory.TokenDeOperador());
        if (metodo == "POST")
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task TokenAssinadoComOutraChave_Retorna401()
    {
        var client = _factory.CreateClient();
        var token = ApiEmMemoriaFactory.GerarToken(
            "chave-totalmente-diferente-com-32-caracteres!!", "Admin", DateTime.UtcNow.AddHours(1));

        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/Incidentes/999");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task TokenExpirado_Retorna401()
    {
        var client = _factory.CreateClient();
        var token = ApiEmMemoriaFactory.GerarToken(
            ApiEmMemoriaFactory.Chave, "Admin", DateTime.UtcNow.AddMinutes(-5));

        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/Incidentes/999");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}