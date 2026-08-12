using Mobilidade.IncidentesConsole.Consultas;
using Mobilidade.IncidentesConsole.Importacao;

var caminhoCsv = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "dados", "incidentes.csv");

if (!File.Exists(caminhoCsv))
{
    Console.Error.WriteLine($"CSV não encontrado: {caminhoCsv}");
    return 1;
}

var resultado = await CsvIncidenteImportador.ImportarAsync(caminhoCsv);

Console.WriteLine($"Importados {resultado.Incidentes.Count} incidentes de '{Path.GetFileName(caminhoCsv)}'.");

if (resultado.Avisos.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"Avisos ({resultado.Avisos.Count} linha(s) descartada(s)):");
    foreach (var aviso in resultado.Avisos)
    {
        Console.WriteLine($"  - {aviso}");
    }
}

ImprimirSecao("Total de incidentes por tipo");
foreach (var c in RelatorioConsultas.TotalPorTipo(resultado.Incidentes))
{
    Console.WriteLine($"  {c.Chave,-18} {c.Quantidade}");
}

ImprimirSecao("Top 3 bairros com mais incidentes de severidade alta");
foreach (var c in RelatorioConsultas.Top3BairrosSeveridadeAlta(resultado.Incidentes))
{
    Console.WriteLine($"  {c.Chave,-18} {c.Quantidade}");
}

ImprimirSecao("Média de incidentes por dia da semana");
foreach (var (dia, media) in RelatorioConsultas.MediaPorDiaDaSemana(resultado.Incidentes))
{
    Console.WriteLine($"  {NomeDia(dia),-10} {media:0.00}");
}

ImprimirSecao("Resumo por bairro (total / severidade alta / tipo predominante)");
foreach (var r in RelatorioConsultas.ResumoPorBairro(resultado.Incidentes))
{
    Console.WriteLine($"  {r.Bairro,-18} total={r.Total,-3} alta={r.SeveridadeAlta,-3} predominante={r.TipoPredominante}");
}

return 0;

static void ImprimirSecao(string titulo)
{
    Console.WriteLine();
    Console.WriteLine(titulo);
    Console.WriteLine(new string('-', titulo.Length));
}

static string NomeDia(DayOfWeek dia) => dia switch
{
    DayOfWeek.Monday => "Segunda",
    DayOfWeek.Tuesday => "Terça",
    DayOfWeek.Wednesday => "Quarta",
    DayOfWeek.Thursday => "Quinta",
    DayOfWeek.Friday => "Sexta",
    DayOfWeek.Saturday => "Sábado",
    DayOfWeek.Sunday => "Domingo",
    _ => dia.ToString()
};
