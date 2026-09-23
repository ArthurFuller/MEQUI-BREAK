# Rush Balance — versão portrait

## Como testar

1. Abra e execute `Assets/Scenes/Boot/Boot.unity`.
2. Entre no Rush Balance pelo Hub.
3. Toque em **Começar**.
4. Toque nos balões dos clientes para inserir pedidos na fila.
5. Arraste os cards para reorganizar a prioridade.
6. Quando aparecer **Toque para entregar**, toque no card pronto.

Não há referência obrigatória para configurar manualmente: todos os objetos e campos usados pelo Rush Balance já estão ligados na Hierarchy da cena.

## Balanceamento inicial

- Partida: 60 segundos.
- Preparo de 1, 2 e 3 itens: 4, 7 e 10 segundos.
- Paciência de cliente paciente, normal e exigente: 35, 30 e 25 segundos.
- Primeiro card: 100% da velocidade.
- Segundo card: 50% da velocidade.
- Terceiro e quarto cards: em espera.
- Recompensa de participação: 20 PB, usando o serviço existente do projeto.

Os valores editáveis ficam nos componentes `RushTimedSessionController` e `RushBalanceController` do objeto **Sistemas**.

## Estrutura da cena

- `Canvas_RushBalance/__SceneTransitionRoot/SafeArea`: interface portrait.
- `AreaDeJogo/AreaClientes`: cenário, quatro posições visíveis, entrada e saída.
- `Cliente_1` a `Cliente_5`: pool de quatro clientes visíveis e um reserva.
- `SlotsPedidos`: quatro destinos fixos da fila.
- `CamadaCards/CardPedido_1` a `CardPedido_4`: pool reciclável dos pedidos.
- `Sistemas`: carregamento, sessão cronometrada e gameplay.
- `EventSystem`: entrada por mouse e toque com o Input System já usado no projeto.

Nenhum cliente, card, ícone ou objeto visual é criado por script. Os scripts apenas ativam, desativam, movimentam e atualizam objetos previamente montados na Hierarchy.

## Checklist de Play Mode

- A cena abre em portrait e aceita toque/clique.
- Sempre há quatro clientes visíveis durante a partida.
- A paciência diminui mesmo durante o preparo.
- Um cliente expirado sai sem recompensa e seu pedido desaparece.
- O card pronto permanece na fila até ser tocado.
- Ao entregar, os ícones vão ao cliente correto, ele reage e sai pela direita.
- Cards entram na fila com fade e escala suave; pedidos com vários itens entregam um ícone por vez em arco.
- Balões respondem ao hover/toque e fazem um pequeno pop ao trocar de pedido para fala.
- Após a saída completa há uma pausa curta; então os clientes à esquerda avançam e o novo entra pela esquerda.
- O resultado aparece aos 60 segundos com entregues e não entregues.
- Voltar antes do fim não concede recompensa.
