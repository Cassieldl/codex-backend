using Codex.Application.Interfaces;
using Codex.Domain.Entities;
using Codex.Domain.Enums;
using Codex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codex.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IRedirecionamentoRepositorio"/> — escrita de redirect na mesma
/// transação da troca de slug (I37/I43/I44). Os dois <c>UPDATE</c> são em lote por <c>WHERE</c>
/// (mesmo padrão de <c>IndiceBuscaWriter</c>), sobre a conexão/transação ambiente do
/// <see cref="CodexDbContext"/> — nunca abrem transação própria.
/// </summary>
public sealed class RedirecionamentoRepositorio : RepositorioBase<CodexDbContext>, IRedirecionamentoRepositorio
{
    public RedirecionamentoRepositorio(CodexDbContext contexto) : base(contexto)
    {
    }

    public Task DesativarPorOrigemAsync(string origem, TipoItemIndexado tipoItem, CancellationToken ct) =>
        // tipoitem é coluna TEXTO (RedirecionamentoConfiguration usa HasConversion<string>): o enum
        // precisa ir como nome, senão o parâmetro chega como inteiro e a comparação nunca casa.
        Contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.redirecionamentos SET desativadoem = now()
            WHERE origem = {origem} AND tipoitem = {tipoItem.ToString()} AND desativadoem IS NULL
            """, ct);

    public Task RepontarDestinosAsync(string destinoAntigo, string destinoNovo, TipoItemIndexado tipoItem, CancellationToken ct) =>
        Contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE codex.redirecionamentos SET destino = {destinoNovo}
            WHERE destino = {destinoAntigo} AND tipoitem = {tipoItem.ToString()} AND desativadoem IS NULL
            """, ct);

    /// <summary>
    /// Grava o redirect — reaproveitando a linha da MESMA origem quando ela já existe, inclusive
    /// desativada. <c>uq_codex_redirecionamentos_origem</c> é único GLOBAL (sem filtro por
    /// <c>desativadoem</c>, conferido na <c>InitialCreate</c>), então um <c>INSERT</c> cego
    /// quebraria com 23505/409 no terceiro passo de um ciclo de renomeações (A→B→A→C): a linha
    /// <c>{A→B}</c> que o passo 2 desativou continua ocupando a origem <c>/provedores/A</c>.
    /// Reativar com o destino novo é o mesmo estado final que um <c>INSERT</c> produziria se o
    /// índice fosse parcial — e mantém o histórico numa linha só por origem.
    /// </summary>
    public async Task CriarAsync(Redirecionamento redirecionamento, CancellationToken ct)
    {
        var existente = await Contexto.Redirecionamentos
            .FirstOrDefaultAsync(r => r.Origem == redirecionamento.Origem, ct);

        if (existente is null)
        {
            await Contexto.Redirecionamentos.AddAsync(redirecionamento, ct);
        }
        else
        {
            existente.AtualizarDestino(redirecionamento.Destino);
            existente.Reativar();
        }

        await SalvarAsync(ct);
    }
}
