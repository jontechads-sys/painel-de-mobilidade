using Mobilidade.Domain.Modelos;
using Xunit;

namespace Mobilidade.Tests;

public class SeveridadeTests
{
    [Fact]
    public void Igualdade_DoisSeveridadeComMesmoValor_SaoIguais()
    {
        var a = Severidade.Alta;
        var b = Severidade.Alta;

        Assert.Equal(a, b);
    }

    [Fact]
    public void Igualdade_SeveridadesDiferentes_NaoSaoIguais()
    {
        Assert.NotEqual(Severidade.Alta, Severidade.Baixa);
    }

    [Fact]
    public void DeveriaEscalonar_SeveridadeAltaComMuitoTempo_RetornaTrue()
    {
        var resultado = Severidade.Alta.DeveriaEscalonar(TimeSpan.FromHours(3));

        Assert.True(resultado);
    }

    [Fact]
    public void DeveriaEscalonar_SeveridadeBaixa_RetornaFalseMesmoComMuitoTempo()
    {
        var resultado = Severidade.Baixa.DeveriaEscalonar(TimeSpan.FromHours(10));

        Assert.False(resultado);
    }
}