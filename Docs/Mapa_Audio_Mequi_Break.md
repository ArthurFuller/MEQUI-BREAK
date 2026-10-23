# Mapa de áudio — Méqui Break

Revisão de 23/10/2026. Os clipes ficam em Assets/Audio e as referências são serializadas no AudioManager da cena Boot. O volume e a ativação continuam sob SettingsManager.

## Interface e progressão

| Evento | Método | Clipe | Uso |
|---|---|---|---|
| Toque de botão / abertura de prancheta | PlayClick | ui_click.wav | UIInteractionFeedback, botões ligados pelo AudioManager, perfil e configurações. Cooldown evita clique duplicado. |
| Confirmação de compra / alteração de dados | PlayConfirm | ui_confirm.wav | CustomizationController e SettingsController. Reservado para confirmações de interface. |
| Recompensa de Pontos Break | PlayReward | pb_reward.wav | PointAnimationManager e LevelUpPopupPreview. |
| Conclusão de atividade | PlayCompletion | minigame_complete.wav | Energy Station, Rush Balance e Combo Crew. |
| Arrastar escolha da Energy Station | PlayEnergyDrag | energy_drag_tick | DraggableInteraction. Referência preservada. |
| Login válido | PlayLoginJingle | login_music.mp3 | LoginController. Respeita a preferência de música. |
| Ação inválida / soltura rejeitada | PlayError | ui_reject.wav | Cards do Rush, alocação inválida no Combo e entrega antecipada. Não reutiliza mais o clique normal. |

## Rush Balance

| Evento | Método | Clipe | Decisão |
|---|---|---|---|
| Início da partida | PlaySessionStart | minigame_start.wav | Identidade comum aos inícios dos minigames com sessão cronometrada. |
| Aceitar pedido do cliente | PlayQueueAccepted | rush_queue.wav | Substitui confirmação de interface. |
| Reordenar fila | PlayQueueReordered | rush_reorder.wav | Feedback curto próprio, diferente de aceitar pedido. |
| Pedido fica pronto | PlayReady | order_ready.wav | Sinal ascendente de prontidão; diferente de retorno de funcionário e lavagem. |
| Carimbo de entrega | PlayEffect | rush_order_delivered.wav | Mantido: já tem significado específico. |
| Carimbo de cancelamento | PlayEffect | rush_order_cancelled.wav | Mantido: já difere da entrega. |

## Combo Crew

| Evento | Método | Clipe | Decisão |
|---|---|---|---|
| Início do turno | PlaySessionStart | minigame_start.wav | Substitui confirmação genérica. |
| Alocar ou mover funcionário | PlayWorkerAssigned | combo_assign.wav | Sinal de posicionamento próprio. |
| Passar trabalho à próxima bancada | PlayWorkStage | combo_stage.wav | Muito curto e com volume reduzido, pois se repete com frequência. |
| Entrega concluída | PlayDelivery | combo_delivery.wav | Sinal positivo de três notas. |
| Pedido perdido | PlayOrderFailed | combo_order_failed.wav | Sequência descendente, distinta de toque inválido. |
| Bandejas lavadas | PlayWashed | combo_washed.wav | Sinal leve próprio; não usa o alerta de pedido pronto. |
| Aviso de quebra próxima | PlayIncident | combo_warning.wav | Duas notas de atenção antes da falha. |
| Estação quebrou | PlayStationBreak | combo_station_break.wav | Mantido: efeito próprio já existente. |
| Início da manutenção | PlayRepair | combo_repair_tool.wav | Mantido: efeito próprio já existente. |
| Manutenção finalizada | PlayStationReady | combo_station_ready.wav | Mantido: efeito próprio já existente. |
| Últimos 3 segundos antes da pausa | PlayBreakTick | combo_break_tick.wav | Um toque por segundo; cooldown global evita sobreposição de avisos simultâneos. |
| Funcionário voltou | PlayWorkerReturn | combo_return.wav | Substitui sinal genérico de prontidão. |

## Trilhas

| Cena | Arquivo | Duração | Andamento |
|---|---|---|---|
| Energy Station | energy_station_loop.wav | 45,71 s | 84 BPM |
| Rush Balance / Rush Balance 2 | rush_balance_loop.wav | 35,56 s | 108 BPM |
| Combo Crew | combo_crew_loop.wav | 40 s | 96 BPM |

Trilhas instrumentais originais, mono a 22.050 Hz, importadas como áudio comprimido em memória. O volume de jogo começa em 0,22 no AudioManager; os efeitos continuam em fonte separada. A trilha para ao sair do minigame e volta ao ativar a música nas configurações. O jingle de login pode terminar no HUB.

O gerador determinístico está em Tools/Audio/generate_minigame_audio.py. Ele produz os WAVs sem dependências externas. Mudanças de composição podem ser feitas no gerador e os clipes podem ser substituídos no Inspector da Boot.

## Arquivos antigos

Existem versões WAV/MP3 de alguns efeitos e um ambient_loop.wav anterior. A presença de um arquivo na pasta não comprova inclusão na build: a decisão depende das referências e de Resources. Esta revisão preserva arquivos antigos para não quebrar referências externas; os eventos acima indicam o que está efetivamente ligado no código e na Boot.
