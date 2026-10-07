using System.Globalization;
using System.Text;
using Codex.Domain.Constantes;
using Codex.Domain.Enums;
using Codex.Domain.Exceptions;

namespace Codex.Domain.Entities;

/// <summary>
/// Busca da área interna que não encontrou resultado, registrada para virar conteúdo (escopo
/// funcional v3.1 §9.2). Cada linha é UMA ocorrência: o mesmo termo buscado dez vezes são dez
/// linhas, e é a contagem delas (<see cref="TermoNormalizado"/>) que prioriza a fila.
///
/// <para>
/// Quem registrou e quem resolveu vêm sempre do token — nunca do corpo da requisição. O instante
/// do registro é <see cref="EntidadeAuditada.CriadoEm"/>.
/// </para>
/// </summary>
public class LacunaBusca : EntidadeAuditada
{
    public int Id { get; private set; }

    /// <summary>Token de concorrência otimista (xmin) — sustenta o If-Match da mudança de estado.</summary>
    public uint Versao { get; private set; }

    public string Termo { get; private set; } = null!;

    /// <summary>Chave de agrupamento: minúsculas, sem acento e com espaços colapsados (<see cref="NormalizarTermo"/>).</summary>
    public string TermoNormalizado { get; private set; } = null!;

    public string? Parceiro { get; private set; }

    public string? NumeroChamado { get; private set; }

    public EstadoLacunaBusca Estado { get; private set; }

    public string? Nota { get; private set; }

    public string RegistradoPorId { get; private set; } = null!;

    public string? RegistradoPorNome { get; private set; }

    public DateTimeOffset? ResolvidaEm { get; private set; }

    public string? ResolvidoPorId { get; private set; }

    public string? ResolvidoPorNome { get; private set; }

    protected LacunaBusca()
    {
    }

    public static LacunaBusca Registrar(
        string termo, string? parceiro, string? numeroChamado, string usuarioId, string? usuarioNome)
    {
        var termoLimpo = (termo ?? string.Empty).Trim();
        if (termoLimpo.Length < LimitesLacunaBusca.TermoMinimo || termoLimpo.Length > LimitesLacunaBusca.Termo)
            throw new ArgumentException(
                $"O termo precisa ter entre {LimitesLacunaBusca.TermoMinimo} e {LimitesLacunaBusca.Termo} caracteres.", nameof(termo));

        return new LacunaBusca
        {
            Termo = termoLimpo,
            TermoNormalizado = NormalizarTermo(termoLimpo),
            Parceiro = VazioParaNulo(parceiro, LimitesLacunaBusca.Parceiro, nameof(parceiro)),
            NumeroChamado = VazioParaNulo(numeroChamado, LimitesLacunaBusca.NumeroChamado, nameof(numeroChamado)),
            Estado = EstadoLacunaBusca.Aberta,
            RegistradoPorId = ExigirUsuarioId(usuarioId),
            RegistradoPorNome = LimitarNome(usuarioNome),
        };
    }

    /// <summary>
    /// Resolve, descarta ou reabre. Repetir o estado atual é recusado: seria um no-op que ainda
    /// assim moveria o token de concorrência e trocaria quem resolveu. Reabrir limpa a resolução
    /// inteira — a nota descrevia a resolução que deixou de valer.
    /// </summary>
    public void AlterarEstado(
        EstadoLacunaBusca novoEstado, string? nota, string usuarioId, string? usuarioNome, DateTimeOffset agora)
    {
        if (novoEstado == Estado)
            throw new TransicaoDeEstadoInvalidaException($"A lacuna já está no estado '{Estado}'.");

        if (novoEstado == EstadoLacunaBusca.Aberta)
        {
            Estado = EstadoLacunaBusca.Aberta;
            Nota = null;
            ResolvidaEm = null;
            ResolvidoPorId = null;
            ResolvidoPorNome = null;
            return;
        }

        Estado = novoEstado;
        Nota = VazioParaNulo(nota, LimitesLacunaBusca.Nota, nameof(nota));
        ResolvidaEm = agora;
        ResolvidoPorId = ExigirUsuarioId(usuarioId);
        ResolvidoPorNome = LimitarNome(usuarioNome);
    }

    /// <summary>
    /// Forma comparável do termo: sem acento, minúsculas e espaços colapsados. Hífen e pontuação
    /// ficam — <c>E-1023</c> e <c>e1023</c> são buscas que o motor trata à parte, e fundi-las aqui
    /// esconderia essa diferença de quem prioriza a fila.
    /// </summary>
    public static string NormalizarTermo(string termo)
    {
        ArgumentNullException.ThrowIfNull(termo);

        var decomposto = termo.Normalize(NormalizationForm.FormD);
        var semMarcas = new StringBuilder(decomposto.Length);
        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                semMarcas.Append(caractere);
        }

        var minusculo = semMarcas.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var colapsado = string.Join(' ', minusculo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        // Texto derivado grava numa coluna do mesmo teto do original: a normalização só encurta,
        // mas a guarda mantém o 22001 impossível mesmo se uma regra Unicode surpreender.
        return Cortar(colapsado, LimitesLacunaBusca.Termo);
    }

    private static string ExigirUsuarioId(string usuarioId)
    {
        if (string.IsNullOrWhiteSpace(usuarioId) || usuarioId.Length > LimitesLacunaBusca.UsuarioId)
            throw new ArgumentException("Identificador do usuário ausente ou acima do limite.", nameof(usuarioId));

        return usuarioId;
    }

    private static string? LimitarNome(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : Cortar(nome.Trim(), LimitesLacunaBusca.UsuarioNome);

    private static string? VazioParaNulo(string? valor, int limite, string parametro)
    {
        var limpo = valor?.Trim();
        if (string.IsNullOrEmpty(limpo))
            return null;

        if (limpo.Length > limite)
            throw new ArgumentException($"O valor tem no máximo {limite} caracteres.", parametro);

        return limpo;
    }

    /// <summary>
    /// Corte por posição UTF-16: recua uma unidade quando termina na metade alta de um par
    /// substituto. O Npgsql grava UTF-8 estrito e lança no INSERT diante de um emoji partido.
    /// </summary>
    private static string Cortar(string texto, int limite)
    {
        if (texto.Length <= limite)
            return texto;

        var corte = texto[..limite];
        return char.IsHighSurrogate(corte[^1]) ? corte[..^1] : corte;
    }
}
