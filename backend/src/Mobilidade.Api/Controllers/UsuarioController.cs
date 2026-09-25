using Microsoft.AspNetCore.Mvc;
using Mobilidade.Api.Dtos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.Interfaces;
using Mobilidade.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _repositorio;
    private readonly IHasherDeSenha _hasher;

    public UsuariosController(IUsuarioRepository repositorio, IHasherDeSenha hasher)
    {
        _repositorio = repositorio;
        _hasher = hasher;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult Criar(CriarUsuarioRequest request)
    {
        var papel = Papel.DeNome(request.Papel);
        var email = Email.Criar(request.Email);

        var usuario = Usuario.Registrar(email, request.Senha, papel, _hasher);

        var criado = _repositorio.Criar(usuario);

        var resposta = new UsuarioResponse(criado.Id, criado.Email.Valor, criado.Papel.Nome);
        return CreatedAtAction(nameof(Criar), new { id = criado.Id }, resposta);
    }
}