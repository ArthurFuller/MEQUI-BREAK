# Energy Station — cards e perguntas aprovados

Revisão mais recente: laterais da barra em formato de cápsula conforme referência image(7).png, reaproveitando o sprite branco existente de Combo Crew em Sliced. Raio de 21 unidades para altura 42, largura 645 preservada. Cores amostradas dos sprites originais: fundo #310D09 e preenchimento #F2B705. Apenas dois componentes Image da cena alterados; sem contorno adicional, novos objetos, novas imagens ou mudança na lógica. Conferência estrutural realizada; renderização em Play Mode ainda pendente. Esta revisão substitui somente o formato visual da restauração descrita abaixo.

Estado atual da barra: restaurada integralmente ao backup anterior ao polimento, por solicitação do usuário. Sprites, cores, Image Simple, posição, dimensões e Hierarchy originais recuperados. Removidos contorno, fundo cinza adicional, máscara e margens internas. Algoritmo original de largura restaurado; preservado apenas o callback que exibe o feedback de conclusão ao terminar a animação. Os registros abaixo das tentativas de polimento da barra estão superados por esta restauração.

## Artes aprovadas e utilizadas

As três pranchas fornecidas em `Assets/Art/EnergyStaion` são a origem das artes. Foram extraídos os oito cards grandes de cada prancha, sem redesenhar, redimensionar ou substituir as ilustrações. O papel externo aos contornos foi removido, preservando o branco interno das ilustrações. Os PNGs individuais ficam em `Assets/Art/EnergyStaion/Cards`.

| Origem | Pergunta | Opções |
| --- | --- | --- |
| Prancha1.png — prévia 05 | O que ajudaria o Méqui a recuperar as energias hoje? | Hidratar, Descansar, Alongar, Banheiro |
| Prancha2.png | Que apoio ajudaria o Méqui no trabalho de hoje? | Dúvidas, Prioridades, Reforço, Materiais |
| Prancha3.png — pão corrigido | O que o Méqui gostaria de praticar mais? | Atendimento, Preparo, Montagem, Organização |

São 12 artes coloridas e 12 monocromáticas. Cada par tem referências independentes no Inspector (`Normal Sprite` e `Used Sprite`). A imagem inteira contém ícone, fundo, contorno e legenda em negrito. A imagem é exibida sem distorção, em slots de 200 × 200 unidades, mantendo o layout existente.

## Cena e fluxo implementados

Cena: `Assets/Scenes/EnergyStation/EnergyStation.unity`.

Os 12 cards estão serializados como GameObjects sob `Canvas/SafeArea/InteractionArea/InteractionObjects`, cada um com seu slot e visual. Os quatro objetos existentes foram reaproveitados e os outros oito foram configurados na cena. Nenhum card é instanciado durante o jogo.

O controlador contém três conjuntos no campo `Question Sets`. Cada conjunto referencia seus quatro cards da Hierarchy. Só o conjunto da pergunta atual é ativado. A sessão não tem prazo nem temporizador visível. Mantém duas escolhas distintas, progresso, tutorial, reação do avatar e conclusão/recompensa existentes. O tempo decorrido é usado somente nos registros da sessão.

A cada sessão concluída, a próxima pergunta avança de pausa para apoio, depois aprendizado e novamente pausa. O índice é salvo em `EnergyStation.QuestionIndex`. Abandonar ou redefinir não avança a pergunta.

Cada opção tem sua arte monocromática fixa atrás da colorida. Arrastar move somente o card colorido, revelando o encaixe cinza. Quando a escolha é aceita, a arte colorida é ocultada e o encaixe continua visível. A animação de onda move as duas artes juntas. A segunda escolha bloqueia as interações; ao terminar o preenchimento, aparecem o escurecimento interno e o botão Concluir. Redefinir continua disponível, limpa as escolhas e restaura as artes coloridas.

## Scripts modificados

- `DraggableInteraction.cs`: referências às duas imagens, consumo da arte colorida, encaixe cinza fixo e animação sincronizada. Preserva o arraste e o retorno existentes.
- `EnergyStationController.cs`: conjuntos configuráveis na cena e alternância da pergunta por sessão concluída; sessão sem prazo, progresso com margem interna, escurecimento da bandeja, entrada do botão Concluir e lembrete animado no botão Redefinir.
- `EventLogger.cs`: redefinir limpa escolhas desfeitas e estados de conclusão anteriores, preservando a pergunta e o tempo da sessão.

Os IDs antigos das opções de pausa foram preservados. As opções novas têm IDs distintos com prefixos `energy_support_` e `energy_practice_`. A pergunta e versão são registradas em `EventTypes` como `energy_question:<id>:v1`. O dashboard não foi expandido nesta tarefa: suas categorias existentes continuam mostrando as quatro opções de pausa, enquanto os novos IDs ficam registrados separadamente.

## Validação realizada

- Compilação do conjunto completo de scripts de runtime com Roslyn e as referências/definições do Unity 6000.3.8f1: sem erros; três avisos preexistentes de campos sem uso em Rush Balance/Combo Crew.
- Verificação atual da cena: 378 IDs únicos, sem componentes com dono incorreto ou inconsistências entre pais e filhos. Três perguntas, 12 escolhas e seus pares de sprites.
- Barra original restaurada em 645 × 42, sem margem interna: preenchimento de 0, 322,5 e 645 unidades em 0%, 50% e 100%. Seus dez objetos/componentes coincidem exatamente com o backup anterior ao polimento.
- Conferência visual dos recortes de hidratação, reforço monocromático e montagem com pão corrigido.
- Testes de lógica sobre os scripts compilados: primeira escolha, rejeição de duplicada sem progresso extra, conclusão na segunda escolha distinta, bloqueio de terceira escolha, redefinição/reutilização e descarte de respostas desfeitas com preservação do contexto da pergunta.

Não foi realizado teste de arraste/renderização em Play Mode ou em dispositivo. Esses testes de lógica não substituem a conferência visual e de input dentro do Unity.

## Acabamento visual atual

Os 12 encaixes usam as artes monocromáticas aprovadas, em RectTransforms de 196 × 196 sob cards de 200 × 200. Na amostragem à escala de UI, nenhum pixel opaco do encaixe ficou exposto fora do card colorido em repouso. O antigo pontilhado não é usado nesta cena.

A barra usa o sprite branco arredondado existente `ComboCrew/Polish/order_progress_fill.png`, em modo Sliced, na base preta, no fundo cinza neutro e no preenchimento. Os cantos têm raio externo de 21 e interno de 17 unidades, formando pontas semicirculares com contorno de 4 unidades. O preenchimento usa #F2B705, amostrado da arte original `progress (1).png`; backup anterior ao polimento e Git apontam para a mesma arte original sem tint. O tamanho permanece 645 × 42, com interior 637 × 34. Nenhum PNG foi criado ou alterado.

O contêiner usa #FFB700, amostrado dos pixels do contorno dos cards Rush Balance e Combo Crew do HUB (não da cor do botão de teste). O preenchimento usa sua arte original, conforme a revisão solicitada. A borda do contêiner tem 10 unidades. O painel de escurecimento tem opacidade de 48% e ocupa somente o interior, com as mesmas dimensões e cantos do fundo interno (1009,7894 × 252,7), preservando a borda amarela. A forma arredondada vem de uma cópia sem alteração de pixels do asset existente Dashboard/Visuals/mask_500x90_r12.png, configurada para fatiamento; nenhuma nova ilustração foi produzida.

O botão Concluir sobe 180 unidades em 0,45 s com desaceleração OutCubic, após o preenchimento terminar. O botão Redefinir recebe um pequeno salto periódico. Todos os elementos ficam serializados na Hierarchy.

As alterações estão implementadas. Compilação e dez testes de lógica passaram; renderização, arraste, máscaras e animações ainda precisam de conferência em Play Mode.

Conferência desta correção: composição técnica a partir da textura fatiada em 0%, 50% e 100%, cor amostrada dos assets do HUB, 378 IDs de cena sem inconsistências de componentes/pais. Essa composição não é uma captura do Unity; permanece pendente a conferência em Play Mode.

Revisão da barra: somente três componentes Image alterados na cena; sprite e configuração visual originais do preenchimento recuperados do Git. Tamanho e referências preservados, fundo cinza, base preta, contêiner dos cards inalterado. Conferência técnica em 0%, 50% e 100%, sem inconsistências de componentes. Ainda sem conferência em Play Mode.

Correção dos cantos após retorno do usuário: substituída a deformação do sprite pequeno por fatiamento do sprite arredondado existente. Escala horizontal das pontas calculada levando em conta sprite PPU 400 e referência Canvas 100; multiplicadores 1/3 externo e 7/17 interno. Conferência técnica com amostragem bilinear da textura em 0%, 50% e 100%; 378 IDs sem inconsistências de componentes/pais. Somente três Images da barra alteradas; sem mudança no contêiner, nos scripts ou nas artes. Renderização em Play Mode ainda não verificada.

Restauração completa da barra: dez registros da cena idênticos ao backup, cinco registros adicionados para contorno/máscara removidos, sem referências pendentes nem inconsistências entre pais e filhos. Compilação sem erros; avisos preexistentes fora do Energy Station. Renderização em Play Mode não verificada. As alterações dos cards e do restante da interface foram preservadas.
