# Combo Crew — relógio de aviso de pausa

## Implementado

A arte aprovada foi importada sem modificar os pixels em `Assets/Art/Characters/Combo crew/HUD/break_clock_approved.png`. O importador define um sprite quadrado de 644 × 644, centrado no relógio, ignorando a margem transparente do PNG original. Sem contorno ou preenchimento circular separado.

Os cinco objetos existentes AvisoPausaCircular foram reaproveitados e renomeados para RelogioAvisoPausa. Cada um possui Image com Filled / Radial360 / Top / Clockwise, cor branca, Preserve Aspect e sem raycast. Posição e tamanho de 100 × 100 preservados. Um Animator serializado foi adicionado a cada relógio.

## Animações editáveis

Assets em `Assets/Art/Characters/Combo crew/Animations`:

- ClockCountdown.anim: Fill Amount de 1 a 0, linear, em 5 s.
- ClockCalm.anim: escala 1, para restaurar o estado normal.
- ClockAlertPulse.anim: escala de 1 a 1,07 e retorno em 0,36 s, em loop.
- BreakClock.controller: camada Countdown sincronizada pelo parâmetro ClockProgress; camada Attention alterna Calm/Alert pelo bool Urgent.

As curvas ficam nos arquivos .anim, editáveis na janela Animation. Nenhum objeto, clipe ou curva é criado durante o jogo. Não há shader personalizado nem sequência DOTween para o relógio.

## Integração mínima

ComboCrewWorkerView.ShowBreakWarning fornece ao Animator o progresso derivado da contagem existente e ativa o pulso quando resta 30% do aviso (1,5 s dos 5 s). SetPaused pausa/retoma o Animator mesmo quando o funcionário não está em uma rota. Ao zerar ou cancelar, o relógio é ocultado. O fluxo de pausa, o balão, a condição de saída segura e a duração da ausência permanecem os existentes.

Campos editáveis no Inspector: Break Warning Animator e Break Alert Fraction. O pulso afeta apenas o relógio, não o ator inteiro.

## Validação

Compilação do conjunto completo de scripts sem erros, com três avisos preexistentes fora desta alteração. Cena verificada sem inconsistências de componentes ou de relações pai/filho; cinco referências de Image e Animator configuradas. O SHA-256 do PNG importado é igual ao do PNG aprovado. Validação dos clipes executada no Unity 6000.3.8f1 em projeto isolado: 19 verificações passaram, cobrindo importação de todos os assets, sprite recortado, curvas editáveis, contagem em 0/2,5/5 s, sincronização pelo Animator, pulso, retorno à escala normal, loop, pausa/retomada e reativação. Os arquivos de animação testados são os mesmos importados no projeto. Não foi executado um turno completo de Combo Crew em Play Mode nem teste em dispositivo.
