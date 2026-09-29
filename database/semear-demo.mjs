/**
 * Semeia um acervo de DEMONSTRAÇÃO no ambiente local, pela API interna — nunca por INSERT direto,
 * para que as regras de negócio (slug, publicação em cascata, índice de busca) sejam as mesmas que
 * o produto aplica.
 *
 * Uso, com a API no ar e um token de `codex.dev`:
 *   node database/semear-demo.mjs <token>
 *
 * Conteúdo fictício por definição: provedores são nomes reais do mercado, mas códigos de erro,
 * mensagens, mapeamentos e artigos são inventados — é o que o rodapé do portal declara.
 */

const API = 'http://localhost:5000/api/v1'
const token = process.argv[2]

if (!token) {
  console.error('Uso: node database/semear-demo.mjs <token>')
  process.exit(1)
}

async function chamar(metodo, rota, corpo, etag) {
  const cabecalhos = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
  if (etag) cabecalhos['If-Match'] = etag

  const resposta = await fetch(`${API}${rota}`, {
    method: metodo,
    headers: cabecalhos,
    body: corpo ? JSON.stringify(corpo) : undefined,
  })

  const texto = await resposta.text()
  if (!resposta.ok) {
    throw new Error(`${metodo} ${rota} -> ${resposta.status}: ${texto.slice(0, 300)}`)
  }

  const dados = texto ? JSON.parse(texto) : null
  return { dados: dados?.data ?? dados, etag: resposta.headers.get('etag') }
}

/** Publicar exige `If-Match` com a versão de concorrência atual. */
async function publicar(rota, id, etag) {
  return chamar('PUT', `${rota}/${id}/publicacao`, { publicado: true }, etag)
}

const versaoLayout = await chamar('POST', '/interno/layout/versoes', {
  rotulo: '4.12',
  descricao: 'Grupo de retenções revisado e novo campo de código de tributação municipal.',
})
console.log('versão de layout 4.12 criada')

const camposCanonicos = [
  ['Razão social do prestador', '//Prestador/RazaoSocial', 'Nome empresarial do prestador de serviço, como consta no cadastro municipal.', 'Prestador', 10],
  ['Inscrição municipal do prestador', '//Prestador/InscricaoMunicipal', 'Número de inscrição do prestador no cadastro mobiliário do município.', 'Prestador', 20],
  ['CNPJ do tomador', '//Tomador/Cnpj', 'Documento do tomador do serviço. Quando pessoa física, o campo de CPF é usado no lugar.', 'Tomador', 30],
  ['Código do serviço', '//Servico/CodigoServico', 'Código do item da lista de serviços da LC 116, no formato adotado pelo município.', 'Serviço', 40],
  ['Valor dos serviços', '//Servico/Valores/ValorServicos', 'Valor bruto dos serviços prestados, antes de deduções e retenções.', 'Serviço', 50],
  ['Alíquota do ISS', '//Servico/Valores/Aliquota', 'Alíquota aplicada sobre a base de cálculo do ISS, conforme o município.', 'Serviço', 60],
  ['ISS retido na fonte', '//Servico/Valores/IssRetido', 'Indica se o ISS é retido pelo tomador. Muda a composição do valor líquido.', 'Retenções', 70],
  ['Código de tributação municipal', '//Servico/CodigoTributacaoMunicipio', 'Código próprio do município, quando ele não adota a lista da LC 116 sem adaptações.', 'Serviço', 80],
]

const camposCriados = []
for (const [nome, caminhoXml, significado, grupo, ordem] of camposCanonicos) {
  const campo = await chamar('POST', `/interno/layout/versoes/${versaoLayout.dados.id}/campos`, {
    nome,
    caminhoXml,
    significado,
    grupo,
    ordem,
  })
  camposCriados.push(campo)
  await publicar('/interno/campos-layout', campo.dados.id, campo.etag)
}
console.log(`${camposCriados.length} campos canônicos criados e publicados`)

const layoutPublicado = await publicar('/interno/layout/versoes', versaoLayout.dados.id, versaoLayout.etag)
await chamar('PUT', `/interno/layout/versoes/${versaoLayout.dados.id}`, { estado: 'Vigente' }, layoutPublicado.etag)
console.log('versão de layout publicada e marcada como vigente')

const provedores = [
  { nome: 'IPM Sistemas', identificadorInvoicy: 'INV-IPM', categoria: 'Proprio', aliases: ['IPM', 'Atende.net'] },
  { nome: 'Betha Sistemas', identificadorInvoicy: 'INV-BETHA', categoria: 'Proprio', aliases: ['Betha'] },
  { nome: 'Nacional/ADN', identificadorInvoicy: 'INV-ADN', categoria: 'Nacional', aliases: ['ADN', 'Padrão Nacional'] },
]

const estados = ['Obrigatorio', 'Opcional', 'Condicional']
const versoesCriadas = []

for (const [indice, dados] of provedores.entries()) {
  const provedor = await chamar('POST', '/interno/provedores', dados)

  const versao = await chamar('POST', `/interno/provedores/${provedor.dados.id}/versoes`, {
    versaoLayoutId: versaoLayout.dados.id,
    rotulo: indice === 0 ? '2.04' : indice === 1 ? '1.11' : '1.00',
  })

  for (const [posicao, campo] of camposCriados.entries()) {
    await chamar('POST', `/interno/versoes/${versao.dados.id}/campos`, {
      campoLayoutId: campo.dados.id,
      tipo: posicao % 3 === 0 ? 'Texto' : posicao % 3 === 1 ? 'Numérico' : 'Decimal',
      tamanho: posicao % 3 === 0 ? '150' : '15',
      mascara: posicao % 3 === 2 ? '0.0000' : null,
      estadoPreenchimento: estados[(posicao + indice) % estados.length],
      observacao:
        posicao === 2
          ? 'Quando o tomador é pessoa física, este provedor exige o campo de CPF preenchido e o de CNPJ ausente — enviar os dois causa rejeição.'
          : null,
      ordem: posicao + 1,
    })
  }

  const versaoPublicada = await publicar('/interno/versoes', versao.dados.id, versao.etag)
  versoesCriadas.push(versao.dados.id)

  await chamar('POST', `/interno/versoes/${versao.dados.id}/exemplos`, {
    nome: indice === 0 ? 'NFS-e com ISS retido' : 'Emissão simples, sem retenção',
    descricao:
      indice === 0
        ? 'Cenário com retenção na fonte: repare que o valor líquido já vem descontado.'
        : 'Cenário mais comum de homologação, sem retenções e com tomador pessoa jurídica.',
    xml: `<Rps>\n  <Prestador>\n    <RazaoSocial>Empresa Exemplo LTDA</RazaoSocial>\n  </Prestador>\n  <Servico>\n    <CodigoServico>14.01</CodigoServico>\n    <Valores>\n      <ValorServicos>1500.00</ValorServicos>\n      <IssRetido>${indice === 0 ? '1' : '2'}</IssRetido>\n    </Valores>\n  </Servico>\n</Rps>`,
  })

  if (indice === 0) {
    const aviso = await chamar('POST', `/interno/versoes/${versao.dados.id}/avisos`, {
      severidade: 1,
      titulo: 'Alíquota de ISS ignorada quando o serviço é do grupo 14',
      corpo: 'Para os itens 14.xx o provedor recalcula a alíquota a partir do cadastro municipal e ignora o valor enviado no XML. Não é erro de integração.',
      vigenciaInicio: new Date().toISOString().slice(0, 10),
      campoVersaoIds: [],
    })
    await publicar('/interno/avisos', aviso.dados.id, aviso.etag)
  }

  await chamar('PUT', `/interno/provedores/${provedor.dados.id}/publicacao`, { publicado: true }, provedor.etag)
  console.log(`provedor ${dados.nome} criado, com versão, campos, exemplo e publicação`)
  void versaoPublicada
}

const erros = [
  {
    codigo: 'E-1023',
    origem: 'Prefeitura',
    versoesProvedorIds: versoesCriadas,
    titulo: 'Inscrição municipal do prestador não encontrada',
    variantes: ['Inscrição municipal inválida ou não cadastrada', 'Prestador não localizado no cadastro mobiliário'],
    causaProvavel: 'A inscrição enviada não existe no cadastro do município, ou a empresa está com a inscrição suspensa.',
    solucaoSugerida: 'Confira a inscrição municipal no cadastro mobiliário do município. Se estiver suspensa, a regularização é feita na prefeitura — não há contorno pelo XML.',
  },
  {
    codigo: 'E-2145',
    origem: 'InvoiCy',
    versoesProvedorIds: versoesCriadas.slice(0, 1),
    titulo: 'ISS retido informado sem o CNPJ do tomador',
    variantes: ['Tomador obrigatório quando IssRetido = 1', 'Retenção exige identificação do tomador'],
    causaProvavel: 'O XML declara retenção na fonte mas não identifica quem retém.',
    solucaoSugerida: 'Preencha o documento do tomador. Com ISS retido, a identificação deixa de ser opcional.',
  },
]

for (const erro of erros) {
  const criado = await chamar('POST', '/interno/erros', erro)
  await publicar('/interno/erros', criado.dados.id, criado.etag)
}
console.log(`${erros.length} erros conhecidos criados e publicados`)

const artigos = [
  {
    tipo: 'Conceito',
    titulo: 'O que o Layout InvoiCy é, e o que ele não é',
    corpo: '## Vocabulário, não comportamento\n\nO Layout InvoiCy define **nome, caminho no XML e significado** de cada campo. Ele não define tipo, tamanho, máscara nem obrigatoriedade: isso é decidido inteiramente por cada provedor, em cada versão.\n\nNão existe um padrão do qual os provedores se desviem. A tabela do provedor é a única autoridade sobre comportamento.\n\n## Por que isso importa na integração\n\nQuando um campo é aceito por um provedor e rejeitado por outro, não há bug: há duas regras diferentes para o mesmo vocabulário.',
  },
  {
    tipo: 'Procedimento',
    titulo: 'Identificar a versão do provedor pelo retorno',
    corpo: '## Quando você não sabe a versão\n\nO retorno do provedor costuma trazer a versão do layout no envelope da resposta. Procure por `versao`, `version` ou `layoutVersion` antes de abrir chamado.\n\n## Se o retorno não disser\n\n1. Consulte a página do provedor aqui no Codex e compare os campos obrigatórios.\n2. Uma diferença de obrigatoriedade costuma ser suficiente para distinguir duas versões.\n3. Persistindo a dúvida, o InvoiCy registra a versão negociada no log de transmissão.',
  },
]

for (const artigo of artigos) {
  const criado = await chamar('POST', '/interno/artigos', artigo)
  await publicar('/interno/artigos', criado.dados.id, criado.etag)
}
console.log(`${artigos.length} artigos criados e publicados`)

const comunicado = await chamar('POST', '/interno/comunicados', {
  severidade: 2,
  abrangencia: 0,
  titulo: 'Transmissão intermitente no ambiente de homologação',
  corpo: 'Estamos observando lentidão e timeouts esporádicos na homologação de alguns municípios. A produção não é afetada. Previsão de normalização até sexta-feira.',
  expiraEm: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
})
await publicar('/interno/comunicados', comunicado.dados.id, comunicado.etag)
console.log('comunicado global publicado')

console.log('\nacervo de demonstração semeado.')
