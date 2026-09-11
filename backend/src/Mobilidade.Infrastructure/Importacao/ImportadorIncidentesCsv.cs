using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Mobilidade.Application.Importacao;
using Mobilidade.Domain.Entity;
using Mobilidade.Domain.ValueObjects;


namespace Mobilidade.Infrastructure.Importacao;

public sealed class ImportadorIncidentesCsv : IIncidenteImportador
{
    private readonly string _caminho;

    public ImportadorIncidentesCsv(IConfiguration configuracao)
    {
        _caminho = configuracao["ImportacaoCsv:Caminho"]
            ?? throw new InvalidOperationException("Caminho do CSV não configurado (chave ImportacaoCsv:Caminho).");
    }

    public async Task<ResultadoImportacao> ImportarAsync(CancellationToken ct = default)
    {
        var incidentes = new List<Incidente>();
        var avisos = new List<string>();
        var numeroLinha = 0;

        await foreach (var linha in File.ReadLinesAsync(_caminho, ct))
        {
            numeroLinha++;

            if (numeroLinha == 1 || string.IsNullOrWhiteSpace(linha))
            {
                continue;
            }

            var campos = DividirLinhaCsv(linha);
            if (campos.Length < 6)
            {
                avisos.Add($"Linha {numeroLinha}: {campos.Length} campo(s), esperado 6 — descartada.");
                continue;
            }

            if (!int.TryParse(campos[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                avisos.Add($"Linha {numeroLinha}: id '{campos[0]}' invalido — descartada.");
                continue;
            }

            if (!DateTime.TryParse(campos[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHora))
            {
                avisos.Add($"Linha {numeroLinha}: data_hora '{campos[1]}' invalida — descartada.");
                continue;
            }

            if (!TryParseEnum<TipoIncidente>(campos[2], out var tipo))
            {
                avisos.Add($"Linha {numeroLinha}: tipo '{campos[2]}' desconhecido — descartada.");
                continue;
            }
            if (!Severidade.TryParse(campos[3], out var severidade))
            {

                avisos.Add($"Linha{numeroLinha}: severidade '{campos[3]}'invalida ou ausente - descartada.");
                continue;
            }
            var nomeBairro = string.IsNullOrWhiteSpace(campos[4]) ? "(nao informado)" : campos[4].Trim();
            var descricao = string.IsNullOrWhiteSpace(campos[5]) ? null : campos[5].Trim();

            // TODO: resolver nomeBairro -> BairroId real (dívida registrada em DECISOES.md).
            // Por enquanto usa 0 como placeholder, o que falha na FK se não existir
            // Bairro com Id = 0 no banco (dívida conhecida, não bloqueante para a semana 5).
            var incidente = Incidente.Abrir(
                dataHora,
                severidade,
                bairroId: 0,
                descricao: descricao,
                orgaoResponsavel: "Nao informado (Importacao CSV)",
                situacao: "Nao informado (Importacao CSV)");

            incidente.Id = id;
            incidente.Tipo = tipo;

            incidentes.Add(incidente);

        }

        return new ResultadoImportacao(incidentes, avisos);
    }

    private static bool TryParseEnum<TEnum>(string valor, out TEnum resultado) where TEnum : struct, Enum
        => Enum.TryParse(valor.Trim(), ignoreCase: true, out resultado);

    private static string[] DividirLinhaCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (c == '"')
            {
                if (dentroDeAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    dentroDeAspas = !dentroDeAspas;
                }
            }
            else if (c == ',' && !dentroDeAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos.ToArray();
    }
}