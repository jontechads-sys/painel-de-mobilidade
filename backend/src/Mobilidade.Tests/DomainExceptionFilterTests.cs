using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Mobilidade.Api.Filtros;
using Mobilidade.Domain.Excecoes;
using Xunit;

namespace Mobilidade.Tests;

public class DomainExceptionFilterTests
{
    private static ExceptionContext ContextoCom(Exception excecao)
        => new(
            new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor()),
            new List<IFilterMetadata>())
        {
            Exception = excecao
        };

    [Fact]
    public void OnException_ComDomainException_Retorna422ComMensagemDoDominio()
    {
        var contexto = ContextoCom(new DomainException("Incidente já foi encerrado"));

        new DomainExceptionFilter().OnException(contexto);

        var resultado = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(422, resultado.StatusCode);
        Assert.True(contexto.ExceptionHandled);

        var problema = Assert.IsType<ProblemDetails>(resultado.Value);
        Assert.Equal("Incidente já foi encerrado", problema.Detail);
    }

    [Fact]
    public void OnException_ComKeyNotFound_Retorna404()
    {
        var contexto = ContextoCom(new KeyNotFoundException("Id inexistente no banco"));

        new DomainExceptionFilter().OnException(contexto);

        var resultado = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(404, resultado.StatusCode);
    }

    [Fact]
    public void OnException_ComExcecaoDesconhecida_NaoTrata()
    {
        var contexto = ContextoCom(new InvalidOperationException("erro qualquer"));

        new DomainExceptionFilter().OnException(contexto);

        Assert.Null(contexto.Result);
        Assert.False(contexto.ExceptionHandled);
    }
}