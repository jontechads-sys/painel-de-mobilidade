using Microsoft.AspNetCore.Mvc;
using Mobilidade.Application.Repositorio;
using Mobilidade.Domain.Modelos;

namespace Mobilidade.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentesController : ControllerBase
{
    private readonly IIncidenteRepository _repositorio;
    public IncidentesController (IIncidenteRepository repositorio)
    {   
        _repositorio = repositorio;
    }
}