# Auditoria de qualidade das imagens

## Resumo

- Foram encontrados 105 arquivos PNG.
- 81 têm pelo menos uma dimensão abaixo de 128 px e 66 abaixo de 64 px.
- 92 estavam com compressão padrão, 13 sem compressão.
- 92 estavam importados como `Sprite Mode: Multiple` e 13 como `Single`.
- Não há `SpriteAtlas` no projeto.
- Apenas 14 texturas têm referências serializadas nas cenas atuais. As demais aparecem apenas no instalador de arte ou não estão conectadas.

## Correções aplicadas

- Filtro dos dez sprites modulares do avatar alterado de `Point` para `Bilinear`, evitando blocos ao ampliar arte não pixelada.
- Filtro de `01-principal-circulo 1.png`, `app-subtitle.png` e `logo-container.png` alterado de `Trilinear` para `Bilinear`; mipmaps já estão desativados, portanto o filtro trilinear não trazia benefício.
- Compressão padrão desativada em `tela-login.png` e nas duas versões de `monster-character`, eliminando um estágio adicional de artefatos nos três sprites pequenos atualmente mais ampliados.
- Mipmaps continuam desativados, transparência continua habilitada e o wrap continua em `Clamp`, configurações adequadas para UI 2D.

## Fontes que precisam ser reexportadas

Alterar o importador não recupera detalhes ausentes no PNG original. Para uma correção visual definitiva, substitua as fontes abaixo mantendo o mesmo nome de arquivo e área transparente.

| Arquivo | Fonte atual | Exibição observada | Problema | Reexportação recomendada |
|---|---:|---:|---|---:|
| `UI/TelaLogin/tela-login.png` | 393×680 | 978×2118 | ampliação de 2,5× a 3,1×, aspecto não uniforme e texto incorporado | pelo menos 1080×2340; preferir fundo sem texto incorporado |
| `UI/EnergyBreak/monster-character.png` | 120×120 | 360×360 | ampliação de 3× | 720×720 ou vetor convertido em alta resolução |
| `UI/EnergyBreak/monster-character (1).png` | 120×120 | 360×360 | ampliação de 3× | 720×720 ou vetor convertido em alta resolução |
| ícones e rótulos de `UI/EnergyBreak` | vários entre 20 e 120 px | até 2–12× a fonte | perda de definição em telas densas | exportar a 2× do maior tamanho de exibição |
| arte planejada de Hub e Customização | vários abaixo de 128 px | maior que a fonte no instalador | nitidez insuficiente quando a integração for refeita | exportar a 2× do maior tamanho de exibição |

`01-principal-circulo 1.png` não precisa de aumento: a fonte tem 4320×4388, é limitada pelo importador a aproximadamente 2016×2048 e aparece em cerca de 516×516.

## Regras para novas imagens

1. Exportar UI raster no mínimo a 2× do maior tamanho de exibição previsto.
2. Manter texto como TextMesh Pro sempre que possível, sem incorporá-lo ao PNG.
3. Usar `Sprite Mode: Single` para imagens isoladas; usar `Multiple` apenas quando houver recortes efetivamente utilizados.
4. Usar `Bilinear`, sem mipmaps, com transparência e `Clamp` para UI comum; reservar `Point` para pixel art intencional.
5. Criar atlas apenas para grupos realmente usados na mesma tela, depois de remover arte sem referência.

## Observação sobre a integração

O arquivo `ProjectSettings/MequiArtIntegration.state` está na versão 2 e impede que o instalador antigo reaplique automaticamente as referências. A versão não foi incrementada porque isso faria alterações amplas nas cenas e poderia sobrescrever o layout atual. A reinstalação completa de arte deve ser feita deliberadamente pelo menu do projeto somente após as fontes de baixa resolução serem substituídas.
