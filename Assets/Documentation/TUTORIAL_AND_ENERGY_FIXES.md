# Tutorial e Energy Station — implementação e validação

## Tutorial

A interface não é mais criada por código durante o jogo. Na primeira abertura do projeto, `FirstRunGuideHierarchyInstaller` cria e salva a estrutura abaixo em HUB, Minigames e EnergyStation:

```text
Canvas
└── TutorialRoot (Image preta, alpha 0,72)
    ├── Mensagem
    │   ├── Progresso
    │   └── Texto
    └── Pular
        └── Texto

SafeArea/…/ContêinerDoAlvo
├── DestaqueTutorial
└── Alvo
```

O próprio `TutorialRoot` é o único overlay: um painel preto de tela cheia com alpha inicial de 0,72. O valor pode ser alterado diretamente no componente `Image`. Não existem os quatro bloqueios nem recortes calculados.

`Mensagem` e `Pular` são filhos desenhados acima do painel. `DestaqueTutorial` fica no mesmo contêiner do alvo: `QuickAction` na HUB, `MinigameList` em Minigames e `InteractionArea` na Energy Station. Assim, Safe Area e layouts afetam alvo e destaque igualmente. Todos ficam visíveis no modo de edição e podem ser ajustados manualmente pelo Inspector. O código não altera nenhum desses valores durante o jogo.

O card Energy Station também fica salvo dentro de `MinigameList`. O `MinigameSelectionController` usa esse card serializado e não executa `Instantiate` em runtime. A instanciação do prefab acontece somente no Editor, quando o instalador grava o layout na cena.

`FirstRunGuideSceneView` contém somente as referências, duração/ease do pulso e a opção de teste. O destaque usa uma sequência explícita de escala para subir e voltar ao valor original, sem calcular posição ou tamanho.

O controlador persistente agora:

- aguarda apenas a lógica da cena ficar disponível;
- busca somente objetos pertencentes à cena carregada;
- não calcula retângulo, posição, tamanho, padding, offset ou área de recorte;
- mantém `Destaque`, `Mensagem` e textos com raycast desativado;
- usa o painel escuro para bloquear os controles que ficam abaixo dele;
- mantém o `GamesButton`, a lista de minigames e as áreas de card/avatar da Energy Station em `sortingOrder` superior, configurado e salvo nas cenas;
- permite que somente o alvo da etapa e o botão `Pular` recebam interação;
- avança e salva o onboarding apenas após a interação real;
- mantém `executarSempreParaTeste` desativado por padrão.

Para refazer deliberadamente a hierarquia, use `Mequi Break > Tutorial > Reinstall Manual Layout`. Esse comando volta aos valores iniciais e, por isso, não deve ser usado depois de personalizar o layout.

## Energy Station

O estado de sessão agora separa `TimeExpired` de `ReadyToComplete`:

- atingir o tempo limite não mostra nem habilita o botão de conclusão;
- a bandeja é bloqueada e a interface orienta o usuário a redefinir;
- redefinir depois do timeout encerra a tentativa anterior, reinicia cronômetro, cards e progresso e abre uma nova sessão de telemetria;
- a conclusão exige o número real de interações configurado;
- `rewardGranted` e o estado `RewardCollected` impedem crédito repetido;
- sair com uma sessão inacabada registra abandono uma única vez.

## Testes manuais recomendados no Unity

| Caso | Resultado esperado |
|---|---|
| Modo de edição | painel, `DestaqueTutorial`, mensagem, Pular e card Energy Station ficam visíveis e editáveis |
| Primeiro acesso à HUB | demais controles ficam escuros; `GamesButton`, destaque, mensagem e `Pular` ficam claros |
| Clique na HUB | o overlay bloqueia outros botões, mas `GamesButton` recebe o clique |
| Boot → HUB | destaque e `GamesButton` permanecem alinhados após a Safe Area recalcular |
| Minigames | card já existe na Hierarchy, destaque tem aproximadamente 285×285 e o card recebe clique |
| Energy Station | cards e avatar ficam acima do overlay; drag e drop continuam funcionando |
| Timeout com 0 ou 1 interação | botão de concluir ausente; nenhum ponto concedido |
| Redefinir após timeout | timer volta ao início, cards e progresso zeram |
| Duplo clique em concluir | pontos e popup processados uma única vez |
| `executarSempreParaTeste` desativado | guia não reaparece depois de `OnboardingStep = 3` |
