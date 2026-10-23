# Validação das correções mobile

Data: 23/10/2026. Unity 6000.3.8f1, teste em cópia isolada do projeto.

## Resultado

- Compilação do código de jogo e do Editor aprovada.
- 166 verificações de integração aprovadas: limites do scroll, todos os 22 chapéus nas duas cenas Rush, todos os chapéus no Combo, centralização do rosto, tamanho dos cards, relógio/contorno, avisos serializados e referências de áudio.
- Shader de contorno importado e renderizado em Direct3D 11 sem erro. Cor interna, borda preta externa e transparência verificadas por leitura de pixels.
- A malha de contorno passa de 582 vértices / 194 triângulos para 4 vértices / 2 triângulos por Image. Isso mede a redução de geometria, não o ganho de FPS.
- Áudios novos: sem amostras saturadas. As trilhas começam e terminam com amplitude zero para evitar estalo na repetição.
- Cenas alteradas: sem IDs locais duplicados, referências locais ausentes ou relação pai/filho inconsistente.

## Correções de origem

- Contorno: a duplicação circular da imagem foi substituída por shader com material compartilhado, cache de stencil e uma única malha.
- Combo Crew: o Canvas principal agora usa Screen Space Overlay, como HUB e Rush, preservando o fluxo do SceneLoader.
- Configurações: o Content fica ancorado no topo e sua altura é calculada pelos controles ativos; não se desativa a elasticidade para esconder o problema.
- Avatar: o centro do corpo é a origem das camadas irmãs. O rosto não recebe um deslocamento arbitrário; os chapéus usam catálogo mais ajuste individual da cena. Os retratos copiam as transformações proporcionais do personagem.
- Rush: o preenchimento da paciência é reinicializado antes da ativação e permanece acima do fundo preto. O sintoma inicial deve ser conferido também na build.
- Combo: o relógio e seu contorno têm um contêiner comum; pulso e tique-taque começam com 3 segundos restantes. Exclamações e luz estão gravadas na Hierarchy.

## Limites do teste

Não foi gerada uma nova APK e nenhum dispositivo estava conectado ao ADB. A estabilidade de FPS e o tempo dos frames no aparelho permanecem pendentes de um perfil de CPU/GPU e GC na build. A mudança de Canvas foi verificada estruturalmente; o retorno completo ao HUB deve ser conferido no aparelho.

A cópia isolada também revelou erros de importação de SVGs antigos de roupas/NPCs (svgImage nulo). Eles não impediram a validação das cenas e dos PNGs usados nestas correções. Não houve alteração desses SVGs nesta revisão.

## Como editar os chapéus

- Rush: selecione o objeto com RushBalanceController; escolha o chapéu no painel de posicionamento do Inspector.
- Combo: selecione Jogador_Ator, que possui AvatarView; escolha o chapéu no painel de ajustes desta cena. A opção de edição direta permite selecionar a camada do chapéu e usar a Scene View.
- Cada seleção conserva seu deslocamento, escala e rotação. Os ajustes do minigame não alteram o catálogo da customização.
- Salve a cena após editar.


## Navegação, início das partidas e dashboard — 23/10/2026

- Removida a árvore `InicioPartida` das duas cenas do Rush Balance, seus controles e suas referências no controlador. A sessão começa automaticamente após o slide de entrada.
- Corrigida uma condição que bloqueava RushBalance2: o texto opcional de entregas era tratado como requisito para iniciar a partida.
- Os textos dos contadores ficam vazios nas cenas e antes do início; o fundo do temporizador permanece visível. Combo Crew mantém a preparação antes de iniciar.
- Voltar das Configurações agora pertence à SafeArea, fora do conteúdo rolável. O título usa altura fixa, e a rolagem usa os mesmos parâmetros da Customização (elasticidade 0,1; desaceleração 0,135; inércia ligada; sensibilidade 1).
- A pedido do usuário, os fundos, bordas e componentes visuais originais das duas cenas do dashboard foram restaurados. O componente DashboardPanelGraphic e seu Inspector foram removidos.
- A máscara de Pages fica parada; todas as abas usam apenas a entrada horizontal de 64 unidades em 0,34 s, Ease.OutCubic, sem fade. A mesma rotina cobre trocas rápidas e Ações.
- OnDisable apenas cancela a transição e libera sua referência, sem restaurar elementos durante a destruição da cena. OnEnable restaura a aba selecionada ao reativar o painel. A conclusão da transição também verifica a existência do contêiner.

Validação anterior: início automático das duas cenas do Rush, contadores vazios e Voltar fixo nas Configurações. Após restaurar o visual do dashboard, os scripts de jogo e Editor compilam; 65 verificações no Unity 6000.3.8f1 passaram, cobrindo as 20 trocas possíveis entre cinco abas sem fade, duração e máscara estáveis, trocas rápidas, desativação/reativação e destruição da UI antes do encerramento da transição. As duas cenas do dashboard não possuem diferenças em relação ao visual original versionado. Validação no Editor, sem nova build Android.
