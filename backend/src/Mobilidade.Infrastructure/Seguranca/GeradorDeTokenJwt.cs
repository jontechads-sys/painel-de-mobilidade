using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.Interfaces;

namespace Mobilidade.Infrastructure.Seguranca;

public class GeradorDeTokenJwt : IGeradorDeToken
{
    private readonly IConfiguration _config;
    public GeradorDeTokenJwt(IConfiguration config)
    {
        _config = config;
    }
    public string Gerar(Usuario usuario)
    {
        // 1. As claims: as afirmações sobre quem está portando o token
        var claims = new[]
        {
        new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, usuario.Email.Valor),
        new Claim("role", usuario.Papel.Nome),
    };

        // 2. A chave e a assinatura
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Chave"]!));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        // 3. O token
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Emissor"],
            audience: _config["Jwt:Audiencia"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credenciais);

        // 4. Serializa para o formato de três partes
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}