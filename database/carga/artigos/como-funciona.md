> [!RESUMO]
> O Codex reúne os erros de NFS-e no InvoiCy e a correção, os campos por provedor e versão e as particularidades em vigor, sem chamado nem login.

## Comece pela busca

A busca fica no topo de qualquer página. Comece pelo que você tem em mãos:

> [!CARTOES]
> - **Código ou mensagem do erro** | Cole como veio: erro documentado abre direto. | [Ex.: E157](/busca?q=E157)
> - **Nome do campo** | Significado e preenchimento. | [Ex.: TomaCNPJ](/busca?q=TomaCNPJ)
> - **Provedor** | Tudo o que está documentado. | [Ex.: ISS.NET](/busca?q=ISS.NET)

Sem resultado exato, vêm os mais próximos, marcados como aproximados.

## Do erro à solução

```fluxo
Você: cola o código ou a mensagem do erro na busca
Codex: abre o erro documentado, com a correção
Provedor: confira se há comunicado ativo que explique o erro
Suporte: se não resolveu, mande o link da página
```

## Provedor e versão

O preenchimento de um campo (tipo, tamanho, obrigatoriedade) muda com o provedor e a versão do município.

> [!DICA] Mesmos nomes do InvoiCy
> O InvoiCy define provedor e versão de cada município, com os nomes do Codex: copie de um e pesquise no outro.

## Duas tabelas

> [!COMPARATIVO]
> **Layout InvoiCy**
> - Responde: o que é o campo?
> - Mostra nome, posição no XML e significado.
>
> **Tabela do provedor**
> - Responde: como preencher nesta versão?
> - Mostra tipo, tamanho, máscara, obrigatoriedade, condições e campo de destino no XML do provedor.

> [!IMPORTANTE] Divergiu? Vale o provedor
> Se as duas tabelas divergirem, vale a tabela do provedor: é o formato que a prefeitura valida.

> [!DETALHES] Como ler a página da versão
> - Campo fora da tabela não se aplica à versão.
> - Campo em confirmação aparece marcado na página da versão.
> - Data de verificação: quando a Migrate comparou a versão com o provedor real. Mais recente, mais confiável.

## Aviso x comunicado

> [!COMPARATIVO]
> **Aviso**
> - Trata de regra em vigor (ex.: campo que virou obrigatório).
> - Dura até mudar.
> - Aparece na página da versão.
>
> **Comunicado**
> - Trata de situação temporária (ex.: instabilidade na prefeitura).
> - Tem data para sair.
> - Aparece na home e, se couber, no topo do provedor, da versão e do erro.

> [!DICA] Erro novo?
> Veja antes se há comunicado ativo do provedor: ele pode explicar o erro sem mudar a integração.

## Quando chamar o suporte

Procure o suporte quando:

> [!CHECKLIST]
> - O erro não está documentado.
> - A solução indicada não resolveu.
> - O provedor age diferente do descrito.

Nos dois últimos casos, mande o link da página: o suporte vê o que você seguiu e corrige o texto.

Erro, campo, aviso e artigo têm link próprio para guardar e compartilhar.
