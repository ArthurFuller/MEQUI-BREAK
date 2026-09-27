# Combo Crew — auditoria, implementação e cena montada

> **Atualização desta entrega:** `Assets/Scenes/ComboCrew/ComboCrew.unity` foi criada com Hierarchy, bancadas, fila, personagens, prancheta, popup e referências serializadas. A análise original abaixo se refere ao ZIP recebido antes da montagem.

## ATUALIZAÇÃO — ÁREAS MARCADAS NOS PRINTS

- Área verde: fila de pedidos ampliada para 950 × 195 unidades de Canvas, com três cards de 285 × 115. Cada card usa fundo escuro, faixa amarela, ícones estruturados do Rush e barra de paciência. Os contadores não encobrem os cards.
- Área azul: a bancada visual do estágio Entrega é identificada como **Balcão**, ocupa 950 × 205 unidades e fica entre a fila e a Montagem. O funcionário trabalha à esquerda; o contador de bandejas sujas e o ícone da bandeja ficam à direita. A rota de Entrega vai pela direita até uma âncora além do limite da tela e retorna à bancada.
- Área rosa: a imagem de fundo da prancheta foi estendida para baixo da tela quando aberta; botão e cinco cards continuam filhos da mesma estrutura móvel.
- Áreas laranjas: os pontos laterais das bancadas esquerda/direita foram deslocados para fora das frentes dos equipamentos; os cards dos pedidos cresceram e receberam acabamento próximo dos cards do Rush Balance.
- A abertura e o fechamento da prancheta usam DOTween em ambos os casos. Durante o arraste, a posição mundial do card é preservada a cada movimento da prancheta, para que ela feche suavemente sob o dedo.
- Inspeção estática: 937 documentos YAML, 246 GameObjects, todas as referências locais, relações pai/filho e componentes presentes. O Unity Editor não está disponível aqui; legibilidade, toque e animações ainda precisam de teste na cena em Play Mode e em aparelho.

## ATUALIZAÇÃO — ARRASTE, TRANSPORTE E ACESSO PELO HUB

- O card retorna à âncora local da prancheta recolhida antes de a prancheta abrir; sobe junto com ela e permanece visível depois de uma atribuição.
- Os cinco personagens da cena receberam `ComboCrewWorkerStationDrag`, `CanvasGroup` e área de toque no corpo. Depois de atribuídos, podem ser arrastados entre bancadas vagas ou trocar posições se os dois funcionários estiverem livres. Arraste durante transporte continua bloqueado.
- Cada bancada tem uma âncora lateral para entrega da bandeja. Na viagem, a imagem da origem se oculta, aparece nas mãos, some ao chegar à lateral do destino e passa à bancada seguinte enquanto o funcionário retorna.
- Na Entrega, o funcionário leva o pedido além do limite superior, aguarda 2 s, volta com a bandeja e a deixa na bancada. A Louça a coleta pela lateral da Entrega, lava e leva as limpas à lateral do Preparo.
- A fila foi movida acima da Entrega; o topo é identificado como fila, e a Entrega é a bancada de onde o funcionário sai.
- O card do Combo Crew no HUB já possui definição, `SceneLoader` e cena no Build Settings. `MinigameCardView` agora restaura o listener ao retornar ao HUB e registra aviso no Console se faltar configuração ou a cena não constar do perfil de build ativo. **Sem executar o Unity, não foi possível confirmar a causa do toque não recebido no aparelho.**
- Inspeção estática desta versão: 925 documentos YAML, 243 objetos; vínculos de componentes, âncoras, arraste, Build Settings e hierarquia conferidos. Abertura, compilação e toque exigem teste no Unity 6000.3.8f1.

## ATUALIZAÇÃO — PRINTS E AJUSTES DA CENA

- Cinco barras de estação e três barras de paciência usam agora `Image.Type.Simple` com escala horizontal, como no `RushOrderView`. O preenchimento inicia em zero na estação e acompanha o valor lógico recebido do controlador. Paciência começa cheia e diminui com a ordem.
- Os três cards fixos de pedido reutilizam as formas dos ícones de hambúrguer, bebida e batata montadas na Hierarchy do Rush Balance. Os ícones aparecem conforme um, dois ou três itens; o texto de quantidade foi desativado.
- O jogador também tem card arrastável na prancheta (índice zero), além dos quatro funcionários. O botão separado de seleção do jogador está inativo. Os cards têm faixa amarela, fundo escuro, corpo e rosto juntos e legenda curta.
- Corpos e rostos dos cinco personagens de cena têm os mesmos limites de RectTransform; todos os chapéus dessas figuras foram desativados. Personagens sem estação ficam ocultos e surgem quando posicionados atrás da bancada.
- A prancheta fechada fica fora da área útil, com uma aba larga visível no rodapé. Aberta, ocupa a parte inferior da tela. Ao iniciar o arraste, recolhe imediatamente sem tirar o card do dedo, liberando a estação de louça como alvo; ao soltar, abre de novo e o card retorna à sua âncora.
- Estrutura estática após ajustes: 905 documentos de cena, 238 GameObjects, referências de componentes e pais e filhos consistentes. **Abertura e teste de gameplay no Unity 6000.3.8f1 e Android continuam pendentes.**

## BLOCO 1 — AUDITORIA DO PROJETO

Projeto examinado: `MEQUI BREAK SEM COMBO CREW(1).zip`, Unity **6000.3.8f1**, UI em retrato. Inspeção estática do ZIP, dos scripts, cenas, Build Settings, arte e definições; **não houve execução no Unity ou em Android**. O projeto tem 1.069 entradas no ZIP, incluindo diretórios, 65 scripts C# sob `Assets/Scripts` antes das mudanças e sete cenas habilitadas no Build Settings.

- No ZIP original, `RushBalance.unity` e `EnergyStation.unity` existiam e `ComboCrew.unity` não existia. Nesta entrega, `ComboCrew.unity` foi criada e adicionada ao Build Settings; o card do HUB já aponta para `ComboCrewDefinition.asset`.
- `SceneLoader` já considera `ComboCrew` no retorno explícito ao HUB e orienta todas as cenas para portrait (`UsesLandscapeLayout` retorna `false`). O comentário de `MobileRuntimeConfigurator` ainda diz que Rush e Combo usam landscape: **comentário obsoleto**, sem efeito funcional.
- `ResultPopup` existe dentro das cenas existentes; não foi encontrado prefab separado desse popup. Reproduza na nova cena sua estrutura e componentes, sem criar outro script de popup.
- `RushOrderView` usa barras suavizadas e animações DOTween; `HierarchyDragHandle` implementa drag sem instanciar nem trocar de pai. `AudioManager`, `MequiHaptics`, `EventLogger`, `PlayerManager`, `PointsService`, `HubEntryHandler` e `PointAnimationManager` são reutilizáveis.
- Há dois arquivos `HapticFeedback.cs`, mas o de `UI/Haptics` contém somente comentários e não declara classe. **Não há duplicação de tipo nessa versão.** Cenas antigas em `Assets/_Recovery` e `Assets/Scenes/Minigames` persistem no ZIP, mas não estão no Build Settings; não foram removidas por não haver prova de que não servem ao projeto.
- A arte específica do Combo encontrada é `Assets/Art/Combo_Crew.png` (card). **Não foram encontradas artes separadas para as cinco bancadas, bandejas ou prancheta.** Será necessário produzir/selecionar essas imagens ou usar formas e sprites provisórios montados manualmente.

## BLOCO 2 — ANÁLISE DO COMBO CREW ATUAL

Não existe gameplay anterior a migrar ou apagar. `ComboCrewDefinition.asset`, a referência visual do HUB, a âncora de moedas do Combo no HUB e as rotas do `SceneLoader` já apontam para o minigame. Não foram detectadas referências quebradas do Combo porque ainda não há cena para serializá-las. Isso **não equivale a uma validação de GUIDs do projeto inteiro**.

A sessão antiga `MinigameSessionController` possui três turnos, números fixos de pedidos e orientação padrão landscape. Ela não atende ao fluxo contínuo de pedidos, transporte de bandejas e encerramento após os pedidos ativos. `RushTimedSessionController` tampouco cobre esse fluxo. Por isso o Combo recebeu um controlador de sessão próprio, mantendo os serviços existentes de resultado, pontos, eventos e navegação.

## BLOCO 3 — VISÃO DO NOVO COMBO CREW

Uma tela compacta em retrato: balcão de saída no alto; percurso visual em S com **Entrega → Montagem → Chapa → Preparo** de cima para baixo, correspondendo à ordem lógica **Preparo → Chapa → Montagem → Entrega → Balcão**. A **Lava-louça** fica no quinto ponto do zigue-zague, como apoio às bandejas, e não como etapa obrigatória do pedido antes da entrega. Colocar a lava-louça no caminho do pedido contradiz a retirada da bandeja depois que o cliente é atendido; esta é a mudança de conceito necessária.

O jogador aparece como trabalhador próprio (índice 0). Arrasta seu próprio card da prancheta até uma bancada. Os outros quatro funcionários usam cards equivalentes. Funcionários ficam atrás da arte frontal da bancada. Uma bancada vazia para de produzir; uma estação ocupada pode ser trocada somente quando o funcionário não está transportando uma bandeja.

## BLOCO 4 — GAMEPLAY E FLUXO DOS PEDIDOS

1. Fase de planejamento: 12 segundos ou botão Começar. Funcionários podem ser distribuídos antes de iniciar.
2. A partida começa com um pedido; novos pedidos tentam entrar a cada 6 segundos, até a capacidade dos cards preexistentes. O pedido mais antigo progride em velocidade normal, o segundo em 50%, os seguintes aguardam. Os valores iniciais ficam no Inspector.
3. A etapa avança apenas com funcionário presente. Sua barra usa o progresso lógico entre 0 e 1. Uma etapa terminada espera se a próxima estiver ocupada.
4. Ao sair do Preparo para a Chapa, o estoque de bandejas limpas diminui em uma unidade. O personagem carrega a bandeja visual, vai à próxima bancada e volta. A bandeja reaparece sobre a bancada de destino. Depois percorre Montagem, Entrega e o Balcão.
5. Ao entregar, a bandeja vai para o contador de sujas. Se a paciência terminar, o pedido falha; uma bandeja já reservada também se torna suja. O card mostra um feedback e os cards restantes avançam.
6. Se houver funcionário na Lava-louça, ele coleta o lote de bandejas sujas no balcão, retorna para lavar e leva as limpas ao Preparo. O deslocamento e o tempo de lavagem crescem com o tamanho do lote. Sem funcionário ali, o ciclo aguarda intervenção do jogador.
7. O cronômetro da partida para de criar pedidos ao zerar; os pedidos já ativos terminam ou falham. Só então aparece o resultado. Sair antes de concluir não concede PBs.

**Limite da versão entregue:** bandejas são representadas por elementos de UI montados na cena, sem física ou inventário 3D. O controle de bandejas e pedidos é lógico e verificável pelos números; as bancadas e a prancheta usam formas coloridas e texto, ainda sem ilustrações finais. A cena ainda requer teste e ajustes visuais no Unity Editor.

## BLOCO 5 — UI/UX E LAYOUT PORTRAIT

Conservar o fundo escuro, texto claro e amarelo da referência do Rush Balance. No topo: voltar, título, timer e fila com barras de paciência decrescentes. Logo abaixo: balcão de saída e contador de bandejas. Ao centro: cinco bancadas alternadas entre esquerda/direita, com corredor visível para movimento. Cada uma contém o personagem atrás, superfície à frente e barra própria abaixo. Na base: botão anexado ao mesmo `RectTransform` da prancheta; ao abrir, os cards sobem com ela. O primeiro card da prancheta é o jogador.

A prancheta fechada deixa somente o puxador no rodapé; quando se arrasta um card ela recolhe imediatamente para liberar a Lava-louça. O `CanvasGroup` do painel não intercepta raycasts fechado. Cards têm rosto, função principal e secundária curtas; nomes são opcionais. Os indicadores de pedido e bancada devem ser distinguíveis também por posição e direção da barra.

## BLOCO 6 — GAME FEEL

- Prancheta: `DOAnchorPos`, 0,22 s, `OutCubic` na abertura e `InOutCubic` no fechamento. A tween anterior é interrompida na posição atual para permitir reversão.
- Card: `HierarchyDragHandle` reaproveitado, com escala discreta ao iniciar, retorno `OutCubic`, destaque de bancadas livres, click e vibração configurável. Durante o arraste, detalhes do card podem ceder lugar ao retrato do NPC.
- Funcionário: movimento `InOutCubic` entre âncoras, pequena pausa na entrega, volta à bancada; ícone da bandeja visível durante o transporte.
- Barras: valores vêm do controlador e são suavizados na view, sem alterar a duração real da etapa ou a paciência lógica. A barra da Lava-louça acompanha o ciclo de coleta, lavagem e devolução.
- Pedido: marca de sucesso/falha, punch curto, fade e redução de escala; a fila ocupa o espaço após a saída. Som existente de confirmar/erro e feedback háptico existente.

## BLOCO 7 — PESQUISA E REFERÊNCIAS

Referências efetivamente usadas: código e cena do Rush Balance (drag, fila, progresso e DOTween), Energy Station (resultado), sistemas gerais do ZIP e a imagem de referência do Rush Balance enviada nesta conversa. Para confirmar o uso de `DOAnchorPos`, `SetEase` e `Kill`, consultei a [documentação oficial do DOTween](https://dotween.demigiant.com/documentation.php). Para a configuração manual de proporções, consultei o [manual da Unity sobre Canvas Scaler](https://docs.unity3d.com/Manual/script-CanvasScaler.html). Os mockups anteriores orientam **composição**, não são arte final nem sprites importados. Nenhum guideline operacional da Arcos Dourados foi consultado; os nomes das estações são abstrações de gameplay e não descrevem procedimento corporativo real.

## BLOCO 8 — ESTADO DA IMPLEMENTAÇÃO

1. Concluído: a cena reutiliza Canvas portrait, Safe Area, EventSystem, SceneLoader e ResultPopup do Rush.
2. Concluído: cinco bancadas com fundo, frente, alvo de drop, âncora, status e barra; jogador e quatro funcionários em camada atrás das frentes.
3. Concluído: balcão, três cards de pedidos, prancheta móvel com quatro cards de funcionários, botão do jogador e referências no controlador.
4. Concluído: cena incluída no Build Settings. O card do HUB já aponta para `ComboCrewDefinition.asset` e passa a ficar disponível quando a cena compõe o build.
5. Pendente no Unity Editor: importar e compilar scripts, abrir a cena, verificar o layout e realizar os fluxos de gameplay, toque e navegação em aparelho.
6. Pendente: substituir as formas coloridas e textos de equipamentos por arte final de cada bancada, caso o objetivo seja fidelidade visual aos mockups.

## BLOCO 9 — ALTERAÇÕES DE CÓDIGO

| Arquivo / classe | Responsabilidade e integração |
|---|---|
| `PointsService.cs` / `PointsService.AwardParticipation(int)` | Sobrecarga para permitir os 20 PBs ajustáveis do Combo sem alterar o prêmio configurado para outros minigames. Método original preservado. |
| `ComboCrewController.cs` / `ComboCrewController` | Sessão, pedidos, estágios, estoque/retorno de bandejas, distribuição, resultado e serviços do Boot. Novo porque nenhum controlador atual representa essa mecânica. |
| `ComboCrewStationView.cs` / `ComboCrewStationView` | Alvo de drop, clique para o jogador e barra/estado de cada bancada. |
| `ComboCrewWorkerView.cs` / `ComboCrewWorkerView` | Personagem, posição na bancada, transporte visual de bandeja e rota de lava-louça; pode aplicar o avatar salvo do jogador por `AvatarView`. |
| `ComboCrewWorkerCard.cs` / `ComboCrewWorkerCard` | Reutiliza `HierarchyDragHandle` para os cards fixos da prancheta. |
| `ComboCrewOrderView.cs` / `ComboCrewOrderView` | Card fixo da fila, paciência, deslocamento e feedback de conclusão/falha. |
| `ComboCrewClipboard.cs` / `ComboCrewClipboard` | Um único elemento móvel formado pelo painel e seu botão. |

Scripts **reutilizados sem alteração**: `HierarchyDragHandle` (arraste), `ResultPopup` (`Show(points, delivered, missed)`), `SceneLoader` (transições), `EventLogger` (sessão), `AudioManager`, `MequiHaptics`, `PlayerManager`, `AvatarView`, `HubEntryHandler` e `PointAnimationManager` (PBs no HUB). `RushOrderView` não foi reutilizado diretamente porque carrega rosto de cliente, ordem de preparo e ícones específicos do Rush.

## BLOCO 10 — REFERÊNCIAS SERIALIZADAS NA CENA

As referências abaixo já foram escritas no YAML da cena e verificadas estaticamente. **A importação e a compilação no Unity Editor continuam pendentes.** Use uma única Canvas com `CanvasScaler` responsivo e Safe Area conforme as cenas existentes. Não coloque a layer do popup dentro do `CanvasGroup` que bloqueia gameplay.

| Hierarchy / objeto | Componente e campo | Valor / referência | Resultado esperado |
|---|---|---|---|
| `ComboCrew/Canvas/SafeArea/Controllers/ComboCrew` | `ComboCrewController.sceneLoader`, `resultPopup`, `startButton`, `backButton`, `` | SceneLoader da cena, popup copiado da cena Rush, botões de início e voltar presentes | Começo, retorno, troca do jogador e resultado |
| Mesmo objeto | `preparationPanel`, `timerLabel`, `messageLabel`, `cleanTrayLabel`, `dirtyTrayLabel`, `clipboard` | Objetos de UI correspondentes | Planejamento, HUD e prancheta |
| Mesmo objeto | `stations[0..4]` | Preparo, Chapa, Montagem, Entrega, Lava-louça, nesta ordem **lógica**; a ordem visual é livre | Percurso em S sem alterar código |
| Mesmo objeto | `workers[0..N]`, `workerCards[0..N]`, `cardAnchors[0..N]` | Índice 0 = jogador; `workerCards[0]` e `cardAnchors[0]` pertencem ao jogador. Cada índice >0 tem card e âncora. | Distribuição sem instanciar |
| Mesmo objeto | `orderCards[]`, `orderAnchors[]`, `departureAnchor` | Arrays do mesmo tamanho, mínimo 2; âncora de saída junto ao balcão | Fila e entregas |
| Mesmo objeto | Tempos e limites | Planejamento 12 s; partida 70 s; chegada 6 s; paciência 42 s; etapa 4 s; segundo pedido 0,5; bandejas 4; lavagem 1,4 s por bandeja; PBs 20 | Valores iniciais ajustáveis |
| `.../Stations/Preparo` etc. | `ComboCrewStationView.workAnchor`, `progressFill`, `highlight`, `status`, `trayOnCounter` | Cada um recebe sua âncora atrás da bancada, imagem Simple com escala horizontal, destaque, TMP_Text e ícone | NPC atrás da arte frontal, barra atualizável |
| `.../Workers/Player` | `ComboCrewWorkerView.actor`, `carriedTray`, `playerAvatar` | RectTransform do jogador em layer atrás da arte frontal, ícone filho desativado, `AvatarView` com sprites configurados | Aparência do perfil e transporte |
| `.../Workers/EmployeeN` | `ComboCrewWorkerView.actor`, `carriedTray` | RectTransform e ícone filho por funcionário; o avatar deste NPC é configurado manualmente | Funcionário reutilizável |
| `.../Clipboard/Root` | `ComboCrewClipboard.movingRoot`, `handleButton`, `panelGroup`, `closedPosition`, `openPosition` | O **botão é filho do próprio Root**; painel também; posições ajustadas na Scene View | Botão acompanha a prancheta |
| `.../Clipboard/EmployeeCardN` | `ComboCrewWorkerCard.owner`, `workerIndex`, `portraitWhileDragging`, `cardDetails` e campos herdados `visual`, `canvasGroup`, `canvasRect` | Owner da cena; índice igual ao funcionário; detalhes com rosto/funções curtas; Canvas de interação | Arrastar e cancelar com retorno |
| `.../OrderQueue/OrderN` | `ComboCrewOrderView.card`, `canvasGroup`, `patienceFill`, `itemIcons[3]`, `completedMark`, `failedMark` | Um card por slot, Image Simple com escala horizontal e marcadores existentes mas inicialmente ocultos | Paciência real e feedback |
| `.../ResultPopup` | `ResultPopup` e todas as referências serializadas | Copiar a Hierarchy da cena Rush, incluindo overlay, `CanvasGroup`, painel, botão Continuar, SceneLoader e `ordersSummaryLabel`. Colocar acima da layer de gameplay. | Mesmo popup, botão clicável |
| `Assets/Scenes/ComboCrew/ComboCrew.unity` | Build Settings | Adicionar cena com nome exato `ComboCrew`; card HUB usa `ComboCrewDefinition.asset` | Navegação pelo HUB |

Para profundidade, a cena coloca os `actor` dos trabalhadores em uma layer da Canvas **antes** da layer das frentes das bancadas; âncoras do trabalho ficam atrás das superfícies. No Inspector, deixe os rostos visíveis acima do tampo; os chapéus estão desativados, e cubra aproximadamente o quarto inferior do corpo com a frente da bancada. Ícones carregados são filhos dos trabalhadores, inicialmente inativos. Contador de bandejas limpas fica no Preparo; contador de sujas no Balcão. As artes finais das cinco bancadas e da prancheta **não estão no ZIP**.

## BLOCO 11 — VALIDAÇÃO

- Abrir a cena pela Boot/HUB; console sem referências ausentes, Canvas em retrato e popup por cima de tudo.
- Prancheta abre, fecha e inverte no meio da animação sem perder o botão ou bloquear estações.
- Um card de funcionário volta para sua âncora quando solto fora de uma estação; ao soltar em estação livre, personagem aparece atrás da bancada. Segunda atribuição em bancada ocupada é recusada.
- Arrastar o card do jogador para uma bancada livre muda sua posição. Mudança de funcionário durante transporte deve esperar retorno.
- Pedido só avança com funcionário na estação; barra pausa se a estação esvaziar, chega a 100% antes do transporte e reinicia na etapa seguinte. Segunda ordem progride a 50%, demais aguardam.
- Bandeja limpa diminui ao sair do Preparo; sujas aumentam em entrega/falha após reserva; lava-louça devolve exatamente o lote que retirou. Testar lote de 1 e de 3.
- Paciência diminui, pedido falho não entrega, card sai e fila avança; entrega rápida em sequência não perde callbacks.
- Partida termina apenas após o cronômetro e os pedidos ativos saírem. Saída antecipada não concede PB; conclusão concede **uma vez**, mostra resumo e moedas partem da âncora Combo no HUB.
- Testar toque duplo no popup, mudança de cena durante tween, perda de foco, arraste com dois dedos e pelo menos um Android com recorte/Safe Area.

**Validação realizada aqui:** inspeção estática de 925 documentos Unity YAML e 243 GameObjects; referências locais, componentes, relações entre pais e filhos, GUIDs dos scripts próprios, arrays do controlador e GUID da cena no Build Settings foram conferidos. **Não foi possível compilar no Unity nem executar gameplay** porque o Editor não está disponível. Os itens acima continuam testes a executar.

## BLOCO 12 — RESULTADO ESPERADO

Na cena montada, o jogador vê pedidos no topo e trabalhadores de fato atrás das bancadas alternadas. Distribui NPCs pela prancheta ou muda o próprio avatar entre estações. Progresso, paciência e estoque refletem os estados do jogo; os personagens transportam bandejas, a lava-louça as devolve e o resultado mantém o visual dos outros minigames. Arte final, equilíbrio de tempos e validação em aparelho ainda exigem trabalho no Unity.
