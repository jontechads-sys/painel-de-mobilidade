namespace Mobilidade.IncidentesConsole.Modelos;

/// <summary>
/// Tipo do incidente de trânsito reportado.
/// </summary>
public enum TipoIncidente
{
    Colisao,
    Alagamento,
    Obra,
    BuracoNaVia,
    SemaforoApagado,
    Atropelamento,
    Engarrafamento
}

/// <summary>
/// Severidade classificada do incidente. Fica de fora do CSV às vezes
/// (incidente registrado antes da triagem) — por isso é usada como
/// <c>Severidade?</c> em <see cref="Incidente"/>, nunca como valor obrigatório.
/// </summary>
public enum Severidade
{
    Baixa,
    Media,
    Alta
}

/// <summary>
/// Um incidente de trânsito importado do CSV. É um <c>record</c> —
/// imutável por design: uma vez lido do arquivo, o registro não é alterado;
/// qualquer correção de dado vira uma nova leitura, não uma mutação in-place.
///
/// Três campos são anuláveis, cada um por um motivo diferente e uma decisão
/// diferente de como o importador lida com a ausência (ver
/// <see cref="Importacao.CsvIncidenteImportador"/>):
///   - <see cref="Severidade"/>: pode genuinamente não existir ainda
///     (incidente não triado) — fica <c>null</c>, o registro é mantido.
///   - <see cref="Bairro"/>: sem valor no CSV vira o marcador
///     "(nao informado)", não null — é um problema de qualidade do dado,
///     não uma ausência válida, e assim continua contável em GroupBy sem
///     precisar de tratamento especial para chave nula.
///   - <see cref="Descricao"/>: texto livre genuinamente opcional; null
///     sinaliza "ninguém preencheu", diferente de string vazia.
/// </summary>
public sealed record Incidente(
    int Id,
    DateTime DataHora,
    TipoIncidente Tipo,
    Severidade? Severidade,
    string Bairro,
    string? Descricao);
