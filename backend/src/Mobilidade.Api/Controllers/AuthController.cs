using Microsoft.AspNetCore.Mvc;
using Mobilidade.Api.Dtos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.Interfaces;
using Mobilidade.Domain.ValueObjects;
using Mobilidade.Domain.Excecoes;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioRepository _repositorio;
    private readonly IHasherDeSenha _hasher;
    private readonly IGeradorDeToken _tokenJwt;

    public AuthController(IUsuarioRepository repositorio, IHasherDeSenha hasher, IGeradorDeToken tokenJwt)
    {
        _repositorio = repositorio;
        _hasher = hasher;
        _tokenJwt = tokenJwt;
    }
    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        Email email;
        try
        {
            email = Email.Criar(request.Email);
        }
        catch (DomainException)
        {
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });
        }

        var usuario = _repositorio.BuscarPorEmail(email);

        if (usuario is null)
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });

        if (!usuario.SenhaConfere(request.Senha, _hasher))
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });

        var token = _tokenJwt.Gerar(usuario);

        return Ok(new LoginResponse(token));
    }
}