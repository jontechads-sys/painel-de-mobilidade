using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Mobilidade.Api.Dtos;
using Xunit;

namespace Mobilidade.Tests;

public class ValidacaoBordaTests
{
    private static IEnumerable<ValidationAttribute> AtributosDe(string parametro)
        => typeof(CriarIncidenteRequest)
            .GetConstructors().First()
            .GetParameters()
            .Single(p => p.Name == parametro)
            .GetCustomAttributes<ValidationAttribute>();

    [Theory]
    [InlineData("DataHora")]
    [InlineData("Severidade")]
    [InlineData("BairroId")]
    [InlineData("OrgaoResponsavel")]
    [InlineData("Situacao")]
    public void CriarIncidenteRequest_CampoObrigatorio_TemRequired(string campo)
    {
        Assert.Contains(AtributosDe(campo), a => a is RequiredAttribute);
    }

    [Fact]
    public void CriarIncidenteRequest_Severidade_AceitaNaoTriadaParaQueODominioDecida()
    {
        var range = (RangeAttribute)AtributosDe("Severidade").Single(a => a is RangeAttribute);

        Assert.True(range.IsValid(3));
    }

    [Fact]
    public void CriarIncidenteRequest_BairroId_RejeitaZeroENegativo()
    {
        var range = (RangeAttribute)AtributosDe("BairroId").Single(a => a is RangeAttribute);

        Assert.False(range.IsValid(0));
        Assert.False(range.IsValid(-1));
        Assert.True(range.IsValid(1));
    }
}