/**
 * Grava a estrutura gerada de uma versão do Layout InvoiCy no arquivo versionado ao lado do XSD. O
 * teste do backend valida o XML dele contra o XSD, e o teste da carga confere que ele é a saída do
 * gerador. Rodar de novo quando o XSD, a planilha ou o vocabulário mudarem.
 *
 *   node database/carga/gerar-estrutura-invoicy.mjs 9
 */

import { writeFile } from 'node:fs/promises'

import { arquivoDaEstrutura, gerarEstrutura } from './layout-invoicy.mjs'

const rotulo = process.argv[2]
if (!rotulo) {
  console.error('Uso: gerar-estrutura-invoicy.mjs <rotulo>')
  process.exit(1)
}

await writeFile(arquivoDaEstrutura(rotulo), `${JSON.stringify(await gerarEstrutura(rotulo))}\n`)
console.log(`estrutura da versão ${rotulo} gravada em ${arquivoDaEstrutura(rotulo)}`)
