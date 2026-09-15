namespace Codex.Domain.ValueObjects;

/// <summary>
/// Validação defensiva de path interno antes de virar <c>Location</c> de um redirect 308 —
/// restrição de segurança (achado BAIXO-02 da auditoria de frontend: o valor de <c>url</c>/
/// redirect é injetado direto em <c>&lt;Link to&gt;</c>/<c>&lt;a href&gt;</c> sem validação lá;
/// URL absoluta atravessa verbatim e vira link para fora do domínio). Usada especificamente para
/// <c>codex.redirecionamentos.destino</c>: diferente do campo <c>Url</c> de
/// <c>VersaoResumoPublicoDto</c> (montado a partir de <see cref="Slug"/>, que por construção nunca
/// produz "/" ou "\" dentro de um segmento), o valor de <c>destino</c> é uma string livre, então
/// validamos nos DOIS pontos: antes de redirecionar (leitura, nos controllers públicos
/// <c>ProvedoresController</c>/<c>ErrosController</c>) e antes de persistir
/// (<see cref="Codex.Domain.Entities.Redirecionamento.Criar"/> e
/// <see cref="Codex.Domain.Entities.Redirecionamento.AtualizarDestino"/>).
///
/// <para>
/// <b>Mora no Domain de propósito (achado BAIXO-B07).</b> A garantia estava 100% no sink e o
/// comentário afirmava que também estava na origem — o que era falso. Em vez de copiar a função
/// para o Domain (AP-SECURITY-011: copiar a função não copia a garantia), ela MUDOU de casa: é o
/// mesmo corpo, num único lugar, alcançável tanto pela entidade quanto pelos controllers.
/// </para>
///
/// <para>
/// <b>Bypass corrigido (achado da auditoria, 2026-09-02):</b> o parser de URL do navegador
/// (WHATWG) remove TAB/LF/CR de QUALQUER posição da string antes de interpretá-la. Uma string
/// como <c>"/\t/evil.com"</c> passava por todas as checagens de prefixo abaixo (o TAB ainda está
/// no meio quando <c>StartsWith("//")</c> roda), e só colapsa para <c>"//evil.com"</c> depois,
/// já no navegador — fora do alcance deste validador. A correção é rejeitar caracteres de
/// controle C0 (<c>U+0000</c>–<c>U+001F</c>) e DEL (<c>U+007F</c>) ANTES de qualquer checagem de
/// prefixo — validar a string pré-normalização não protege um sink que a resolve
/// pós-normalização; nunca inverter essa ordem.
/// </para>
/// </summary>
public static class CaminhoInternoSeguro
{
    public static bool EhValido(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
            return false;

        // Caracteres de controle C0/DEL PRIMEIRO — antes de qualquer StartsWith/Contains. São
        // removidos pelo parser WHATWG do navegador antes de resolver a URL, então a string
        // "como está aqui" pode não ser a string que o navegador realmente vai interpretar
        // (ex.: "/\t/evil.com" só vira "//evil.com" depois de normalizada pelo browser).
        foreach (var caractere in caminho)
        {
            if (caractere <= 0x1F || caractere == 0x7F)
                return false;
        }

        if (!caminho.StartsWith('/'))
            return false;

        if (caminho.StartsWith("//") || caminho.StartsWith("/\\"))
            return false;

        if (caminho.Contains("://", StringComparison.Ordinal))
            return false;

        return true;
    }
}
