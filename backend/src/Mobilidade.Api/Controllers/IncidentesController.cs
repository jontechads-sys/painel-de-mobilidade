using Microsoft.AspNetCore.Mvc;
using Mobilidade.Api.Dtos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;
using Mobilidade.Infrastructure.Servicos;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentesController : ControllerBase
{
    private readonly IIncidenteRepository _repositorio;
    private readonly ServicoAberturaIncidente _servicoAbertura;

    public IncidentesController(IIncidenteRepository repositorio, ServicoAberturaIncidente servicoAbertura)
    {
        _repositorio = repositorio;
        _servicoAbertura = servicoAbertura;
    }

    [HttpPost]
    public async Task<IActionResult> Criar(CriarIncidenteRequest request)
    {
        var incidenteCriado = await _servicoAbertura.Executar(
            request.DataHora,
            request.Severidade,
            request.OrgaoResponsavel,
            request.Situacao);

        return CreatedAtAction(nameof(BuscarPorId), new { id = incidenteCriado.Id }, incidenteCriado);
    }

    [HttpGet("{id}")]
    public IActionResult BuscarPorId(int id)
    {
        var incidente = _repositorio.BuscarPorId(id);
        if (incidente is null)
            return NotFound();
        return Ok(incidente);
    }

    [HttpGet]
    public IActionResult Listar()
    {
        var incidentes = _repositorio.Listar();
        return Ok(incidentes);
    }

    [HttpPut("{id}")]
    public IActionResult Atualizar(int id, AtualizarIncidenteRequest request)
    {
        var incidenteExistente = _repositorio.BuscarPorId(id);
        if (incidenteExistente is null)
        {
            return NotFound();
        }

        incidenteExistente.Tipo = request.Tipo;
        incidenteExistente.BairroId = request.BairroId;
        incidenteExistente.Descricao = request.Descricao;

        var incidenteAtualizado = _repositorio.Atualizar(id, incidenteExistente);
        return Ok(incidenteAtualizado);
    }

    [HttpDelete("{id}")]
    public IActionResult Remover(int id)
    {
        var incidenteRemovido = _repositorio.Remover(id);

        if (!incidenteRemovido)
        {
            return NotFound();
        }
        return NoContent();
    }
}