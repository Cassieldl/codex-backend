O Codex reúne o conhecimento acumulado pelo suporte da Migrate sobre a emissão de NFS-e pelo InvoiCy: o significado dos erros de cada provedor, como corrigi-los, como preencher os campos em cada provedor e versão e quais particularidades estão valendo no momento.

A ideia é simples: encontrar, agora, a resposta para algo que já foi resolvido antes, sem precisar abrir um chamado.

O acesso é aberto. Você não precisa fazer login para consultar o conteúdo.

## Comece pelo que você tem em mãos

Quase toda consulta começa por uma destas três informações. Use a busca no topo de qualquer página:

* **Código ou mensagem do erro:** pesquise exatamente como apareceu no retorno. Se o erro estiver documentado, você será levado diretamente à página correspondente.
* **Nome de um campo:** pesquise o campo do XML, como `TomaCNPJ`, para descobrir seu significado e como ele deve ser preenchido.
* **Nome do provedor:** consulte tudo o que está documentado para um determinado provedor.

Se não houver uma correspondência exata, o Codex mostra os resultados mais próximos e identifica quando uma correspondência é aproximada.

## Provedor e versão são parte da resposta

Um mesmo campo pode ter tipo, tamanho ou obrigatoriedade diferentes dependendo do provedor e da versão utilizada pelo município.

Por isso, as informações de preenchimento no Codex sempre estão associadas a um **provedor e uma versão específicos**.

O InvoiCy é quem determina qual provedor e versão atendem cada município. Os nomes usados no Codex são os mesmos do InvoiCy, então você pode copiar o nome de um sistema e pesquisá-lo no outro.

## Entenda as duas tabelas

O Codex usa duas referências diferentes porque elas respondem a perguntas diferentes:

### Layout InvoiCy

Mostra o vocabulário do InvoiCy: o nome do campo, sua posição no XML e o que ele representa. Ele explica **o que é o campo**, mas não como preenchê-lo.

### Tabela da versão do provedor

Mostra **como o campo deve ser preenchido** naquela versão: tipo, tamanho, máscara, obrigatoriedade, condições de preenchimento e para qual campo do XML do provedor o valor é enviado.

Se houver diferença entre as duas referências, siga a tabela do provedor. É o formato aceito e validado pela prefeitura.

## Se o campo não estiver na tabela

A tabela de cada versão representa todos os campos aplicáveis àquela versão.

Se um campo não estiver listado, ele **não se aplica àquela versão**. Isso não significa que a documentação esteja incompleta.

Quando algum campo ainda estiver em confirmação, ele será identificado na própria página da versão. Fora desses casos, o que não estiver na tabela não se aplica.

## Veja quando a informação foi verificada

Cada versão informa a **data da última verificação**.

Ela indica quando alguém da Migrate comparou a documentação com o comportamento real do provedor. Em geral, quanto mais recente essa data, maior a confiança de que a informação ainda corresponde ao ambiente atual.

## Avisos e comunicados

Nem toda informação tem o mesmo prazo de validade.

**Avisos** registram regras ou particularidades que continuam valendo até que sejam alteradas, como um campo que passou a ser obrigatório. Eles ficam na página da versão correspondente.

**Comunicados** tratam de situações temporárias, como instabilidades ou problemas no ambiente da prefeitura. Eles têm uma data para sair do ar e aparecem na página inicial do Codex e, quando aplicável, no topo das páginas do provedor, da versão e do erro.

Por isso, antes de investigar um erro novo, confira se existe algum comunicado ativo para o provedor. Ele pode explicar o comportamento que você está encontrando sem que seja necessário alterar nada na integração.

## Cada informação tem seu próprio link

Erros, campos, avisos e artigos possuem um endereço próprio.

Quando encontrar uma informação útil, guarde ou compartilhe o link. Além de facilitar uma consulta futura, ele permite que você mostre exatamente a mesma referência para outra pessoa da equipe.

## Quando procurar o suporte

O Codex deve ser o primeiro lugar para consultar uma dúvida já conhecida. Procure o suporte da Migrate quando:

* o erro não estiver documentado;
* a solução indicada no Codex não resolver o problema;
* o comportamento do provedor for diferente do descrito na documentação.

Nos dois últimos casos, envie também o **link da página consultada**.

Isso ajuda o suporte a entender rapidamente o que foi seguido e permite identificar se a documentação precisa ser atualizada para evitar o mesmo problema no futuro.
