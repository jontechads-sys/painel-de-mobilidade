using Microsoft.AspNetCore.Mvc;
using Mobilidade.Api.Dtos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentesController : ControllerBase
{
    private readonly IIncidenteRepository _repositorio;
    public IncidentesController(IIncidenteRepository repositorio)
    {
        _repositorio = repositorio;
    }
    [HttpPost]
    public IActionResult Criar(CriarIncidenteRequest request)
    {
        var incidente = new Incidente(request.DataHora)
        {
            Id = 0,
            Tipo = request.Tipo,
            Severidade = request.Severidade,
            BairroId = request.BairroId,
            Descricao = request.Descricao
        };
        var incidenteCriado = _repositorio.Criar(incidente);
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
        var incidenteConvertido = new Incidente(request.DataHora)
        {
            Id = id,
            Tipo = request.Tipo,
            Severidade = request.Severidade,
            BairroId = request.BairroId,
            Descricao = request.Descricao
            };
        var incidenteAtualizado = _repositorio.Atualizar(id, incidenteConvertido);
        if (incidenteAtualizado is null)
        {
            return NotFound();
        }

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