using Microsoft.AspNetCore.Mvc;
using Mobilidade.Api.Dtos;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

using Mobilidade.Application.CasosDeUso;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentesController : ControllerBase
{
    private readonly IIncidenteRepository _repositorio;
    private readonly AbrirIncidente _abrirIncidente;
    private readonly EncerrarIncidente _encerrarIncidente;
    private readonly RegistrarAtendimento _registrarAtendimento;

    public IncidentesController(IIncidenteRepository repositorio, AbrirIncidente servicoAbertura, EncerrarIncidente encerrarIncidente, RegistrarAtendimento registrarAtendimento)
    {
        _repositorio = repositorio;
        _abrirIncidente = servicoAbertura;
        _encerrarIncidente = encerrarIncidente;
        _registrarAtendimento = registrarAtendimento;
    }
    [HttpPost]
    public IActionResult Criar(CriarIncidenteRequest request)
    {
        var severidade = Severidade.DeInt(request.Severidade!.Value);

        var incidenteCriado = _abrirIncidente.Executar(
            request.DataHora!.Value,
            severidade,
            request.BairroId!.Value,
            request.Descricao,
            request.OrgaoResponsavel!,
            request.Situacao!);

        var incidenteCompleto = _repositorio.BuscarPorId(incidenteCriado);
        return CreatedAtAction(nameof(BuscarPorId), new { id = incidenteCriado }, incidenteCompleto);
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
        incidenteExistente.MudarBairro(request.BairroId);
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
    [HttpPost("{id}/encerrar")]
    public IActionResult Encerrar(int id)
    {
        int incidenteId = _encerrarIncidente.Executar(id);
        var incidenteCompleto = _repositorio.BuscarPorId(incidenteId);
        return Ok(incidenteCompleto);
    }

    [HttpPost("{id}/atendimentos")]
    public IActionResult RegistrarAtendimento(int id, RegistrarAtendimentoRequest request)
    {
        int incidenteId = _registrarAtendimento.Executar(
            id, request.DataHora, request.OrgaoResponsavel, request.Situacao);

        var incidenteCompleto = _repositorio.BuscarPorId(incidenteId);
        return Ok(incidenteCompleto);
    }
}

