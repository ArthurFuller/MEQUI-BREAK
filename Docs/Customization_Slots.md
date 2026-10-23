# Customização — cores manuais e contorno de seleção

## Alterações

AvatarOptionButton não escreve mais a cor do Image do slot. Os campos antigos selectedBackgroundColor, unlockedBackgroundColor e lockedBackgroundColor foram removidos. Seleção e bloqueio controlam somente a ativação dos respectivos objetos. O overlay já não recebia tintas por script e foi preservado.

Os 46 Buttons de opção usam Transition = None, para impedir as tintas de hover, clique, foco e desativação. As cores e demais propriedades dos Images dos slots e dos overlays permanecem idênticas às configurações encontradas na cena.

Cada slot tem um filho serializado DestaqueSelecao, inicialmente inativo, com Image e sem raycast. Ele usa o mesmo sprite e os mesmos limites do slot, com âncoras esticadas e deslocamentos zerados. A referência fica no campo Selection Highlight de AvatarOptionButton. O destaque é exibido apenas quando o item está selecionado e desbloqueado. As animações existentes da opção movem o slot e o destaque juntos. Nenhum desses objetos é criado durante o jogo.

## Estilo editável

A cor fica no Image do filho DestaqueSelecao: amarelo #FFB700, podendo ser alterada para branco ou outro tom diretamente no Inspector. A espessura é o parâmetro Outline Width do material SlotSelectionOutline, inicialmente 12 pixels da textura de origem (aproximadamente 5,5 unidades no layout atual de 220 × 211,14).

O material usa um shader UI pequeno, com stencil e recorte de UI. Ele extrai uma borda interna da transparência do próprio sprite, com 16 amostras distribuídas ao redor de cada ponto. A cor original escura da textura não multiplica o amarelo; o interior permanece transparente nessa camada. A borda ocupa o contorno do asset sem expandir o retângulo nem afetar o layout. Nenhum PNG foi criado ou alterado. O shader trabalha com o sprite completo, sem atlas, como os slots atuais.

Arquivos novos: Assets/Art/Customization/SlotSelectionOutline.shader e SlotSelectionOutline.mat. Cena: Assets/Scenes/Customization/Customization.unity. Script: Assets/Scripts/Customization/AvatarOptionButton.cs.

## Validação

- Compilação completa dos scripts de runtime sem erros; três avisos de campos sem uso preexistentes em Rush Balance/Combo Crew.
- Referências de componentes e relações pai/filho da cena verificadas, sem inconsistências.
- 46 contornos configurados, nenhum Image original de slot ou de overlay alterado.
- 11 verificações executadas no Unity 6000.3.8f1, com D3D11, em projeto isolado: importação, seleção, bloqueio, desbloqueio, desmarcação, preservação das cores manuais inclusive após nova edição, compilação do shader, interior sem recoloração e contorno visível apenas no slot selecionado.
- Renderização de teste conferida visualmente; a geometria e os cantos acompanham o asset.

Ainda não foi executada a cena completa de customização em Play Mode nem uma build em dispositivo.

## Sincronização da cena aberta

CustomizationSelectionHierarchyInstaller funciona exclusivamente no Editor. Ao recompilar, abrir a cena ou voltar do Play Mode, adiciona os filhos ausentes e vincula Selection Highlight na cena carregada, sem recarregar a cena nem substituir edições manuais. Também pode ser chamado por Mequi Break > Customização > Sincronizar destaques na Hierarchy. A cena é marcada como modificada; salve com Ctrl+S. Objetos que já têm uma referência válida são preservados.

Teste em cópia da cena: remoção dos 46 filhos, recuperação dos 46 vínculos, seleção/desmarcação, preservação das cores originais, execução repetida sem duplicatas e salvamento. A sincronização da instância interativa do Editor ainda depende da importação do script pelo Unity.

## Entrada em onda das categorias

Correção em CustomizationController: a onda anterior é encerrada antes da troca de visibilidade. O layout é calculado antes de capturar as posições; GridLayoutGroup e ContentSizeFitter são suspensos apenas durante a onda e restaurados ao completar ou cancelar. A rolagem volta ao topo e sua inércia é interrompida. Tocar na aba ativa reinicia a mesma onda. A coroutine da primeira entrada é cancelada ao escolher uma categoria, evitando um segundo início atrasado. Nenhum objeto, asset ou componente foi adicionado à cena nesta correção.

Validação em Unity 6000.3.8f1, em projeto isolado com os métodos de onda extraídos diretamente do código de produção: a versão anterior falhou ao recalcular o layout durante a animação (a grade sobrescreveu a posição intermediária). A versão corrigida passou em 55 verificações, incluindo três categorias, repetição da aba ativa, 45 trocas rápidas, recálculo do layout, tamanho do conteúdo, conclusão, cancelamento e ausência de deslocamento acumulado. Os scripts reais de runtime também foram compilados sem erros. A cena completa em Play Mode e dispositivo ainda não foram testados nesta etapa.
