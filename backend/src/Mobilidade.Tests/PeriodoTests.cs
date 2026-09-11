using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;

using Xunit;

namespace Mobilidade.Tests;

public class PeriodoTests
{
    [Fact]
    public void Igualdade_DoisPeriodosComMesmasDatas_SaoIguais()
    {
        var inicio = new DateTime(2026, 9, 10, 8, 0, 0);
        var fim = new DateTime(2026, 9, 10, 10, 0, 0);

        var a = new Periodo(inicio, fim);
        var b = new Periodo(inicio, fim);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Criar_ComFimAnteriorAoInicio_LancaArgumentException()
    {
        var inicio = new DateTime(2026, 9, 10, 10, 0, 0);
        var fimInvalido = new DateTime(2026, 9, 10, 8, 0, 0);

        Assert.Throws<ArgumentException>(() => new Periodo(inicio, fimInvalido));
    }

    [Fact]
    public void EstaEmAberto_SemFim_RetornaTrue()
    {
        var periodo = new Periodo(DateTime.UtcNow);

        Assert.True(periodo.EstaEmAberto);
    }

    [Fact]
    public void Duracao_ComInicioEFim_CalculaCorretamente()
    {
        var inicio = new DateTime(2026, 9, 10, 8, 0, 0);
        var fim = new DateTime(2026, 9, 10, 10, 30, 0);

        var periodo = new Periodo(inicio, fim);

        Assert.Equal(TimeSpan.FromHours(2.5), periodo.Duracao);
    }
}