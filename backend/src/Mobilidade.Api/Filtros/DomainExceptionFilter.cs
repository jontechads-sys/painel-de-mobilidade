using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Mobilidade.Domain.Excecoes;

namespace Mobilidade.Api.Filtros;

public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        if (contexto.Exception is DomainException dominio)
        {
            contexto.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regra de negócio violada",
                Detail = dominio.Message
            })
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

            contexto.ExceptionHandled = true;
            return;
        }

        if (contexto.Exception is KeyNotFoundException naoEncontrado)
        {
            contexto.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso não encontrado",
                Detail = naoEncontrado.Message
            })
            { StatusCode = StatusCodes.Status404NotFound };

            contexto.ExceptionHandled = true;
        }
    }
}