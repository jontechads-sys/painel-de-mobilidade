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
    IActionResult Criar (CriarIncidenteRequest request)
    {
        var incidente = new Incidente(0,request.DataHora,
        request.Tipo,request.Severidade,request.Bairro,request.Descricao);
        var incidenteCriado = _repositorio.Criar(incidente);
        return Ok(incidenteCriado);
    }
}