# MEQUI BREAK — Responsive UI Standard (Etapa 2)

Este documento é a referência oficial de tela para o projeto. Ele substitui experimentos anteriores de `Match Width`, `Match Height` e reposicionamento global.

## Padrão global congelado

- Orientação: **Portrait normal**, bloqueado.
- Portrait Upside Down: desativado.
- Landscape Left/Right: desativados.
- Resolução de design: **1080 × 1920**.
- Canvas: **Screen Space - Overlay**.
- Canvas Scaler: **Scale With Screen Size**.
- Reference Resolution: **1080 × 1920**.
- Screen Match Mode: **Expand**.
- Reference Pixels Per Unit: **100**.
- Nenhum script de runtime pode alterar orientação, `CanvasScaler`, `referenceResolution`, `screenMatchMode` ou `matchWidthOrHeight`.

## Por que Expand

Em celulares portrait mais altos que 16:9, `Expand` preserva a largura lógica mínima do design e disponibiliza altura adicional para os containers responsivos. Em telas proporcionalmente mais largas (por exemplo, tablets portrait), ele evita sacrificar a área visível que ocorreria com um `Match Width` rígido.

O `CanvasScaler` não é responsável por redistribuir Header, Avatar, Cards ou controles. Essa responsabilidade pertence à estrutura interna de cada tela (Safe Area, regions, anchors semânticos, Layout Groups, Layout Elements e ScrollRect quando necessário).

## Regra de composição

1. Backgrounds e decoração poderão ser full-bleed.
2. Conteúdo funcional deverá respeitar Safe Area (Etapa 3).
3. Elementos devem ser ancorados ao container semanticamente correto, e não a percentuais arbitrários do Canvas inteiro.
4. Espaço excedente deve ser distribuído por regiões flexíveis controladas, não por anchors independentes.
5. Layout não deve ser construído usando `Transform.localScale` diferente de 1 para compensar tamanho. Quando possível, o tamanho visual deve existir no próprio `RectTransform`.
6. Layout Groups controlam containers/base; DOTween deve animar wrappers/visuais quando houver risco de disputa pela mesma propriedade.
7. Scroll é preferível a esmagar conteúdo quando não há espaço suficiente.
8. Não haverá regras específicas por modelo de aparelho. Apenas comportamento responsivo e, se realmente necessário, breakpoint por classe de layout (phone/tablet).

## Presets obrigatórios de validação

| Perfil | Resolução | Proporção aproximada | Uso |
|---|---:|---:|---|
| Baseline | 1080 × 1920 | 16:9 | design de referência |
| Phone alto | 1080 × 2160 | 18:9 | distribuição vertical |
| iPhone moderno | 1179 × 2556 | 19.5:9 | Safe Area/cutout |
| Android comum | 1080 × 2400 | 20:9 | teste principal de celular alto |
| Android muito alto | 1080 × 2520 | 21:9 | limite vertical |
| Phone pequeno | 750 × 1334 | 16:9 | legibilidade/touch |
| High DPI | 1440 × 3200 | 20:9 | escala e densidade |
| Tablet portrait | 1536 × 2048 | 4:3 | largura adicional |

## Workflow de Editor

- `1080×1920` é o quadro de design-base.
- Para prever um aparelho real, usar **Device Simulator** com o aparelho/proporção correspondente.
- `Free Aspect` não é critério de aprovação de mobile.
- A aparência no Simulator deve ser comparada com a build instalada.
- Safe Area será simulada e implementada na Etapa 3; até lá não considerar notch/cutout resolvido.

## Estado da Etapa 2

A Etapa 2 congela apenas o padrão global. Ela **não corrige ainda os gaps internos atuais** de HUB, Customization, Energy Station, Profile ou Settings. Esses gaps são consequência da estrutura interna e serão corrigidos por tela na Etapa 4 depois da Safe Area. Alterar novamente o Canvas Scaler para esconder esses gaps é proibido sem nova decisão arquitetural.
