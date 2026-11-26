# Ensaio da prova de vida na lfw

Gerado por `tools/PontoFacial.EnsaioDaProvaDeVida` em 2026-11-25, em 25 minutos. As fotos e os quadros não estão no repositório; só este relatório.

As taxas são as da ISO/IEC 30107-3: o **APCER** é a fração dos ataques de uma espécie que a prova de vida aceitou; o **BPCER**, a das pessoas de verdade que ela recusou. A norma manda informar o APCER de cada espécie e o da pior, e não a média.

## Como os ataques foram feitos

- 300 apresentações de ataque por espécie, 8 espécies, 2.400 ao todo.
- As fotos são da lfw: a primeira de cada pessoa, quando é de frente (3.237 fotos). A troca de fotos usa as 592 pessoas com quatro fotos ou mais.
- Cada foto é impressa em tamanho real, com os olhos a 63 mm, e vista por uma câmera simulada de 640×480 pixels a 35–45 cm, a 15 quadros por segundo, com o tremor da mão e o ruído do sensor.
- O atacante segue o desafio sorteado na tela e faz o melhor que cada espécie permite. Cada apresentação passa pela prova de vida inteira: o detector, o acompanhamento dos nove pontos e a sessão do domínio.
- Quando a foto não passava por rosto de frente no começo, foi trocada por outra, como faria quem ataca (719 trocas).

| Espécie | Como |
|---|---|
| foto parada | A foto impressa, ou na tela de um celular, segura diante da câmera, só com o tremor da mão. |
| foto plana inclinada | A foto plana inclinada a cada pedido; nos giros, na diagonal, para o nariz andar para o lado como numa cabeça. |
| foto curvada de lado | A foto enrolada de um lado para o outro, com raio de 4 a 10 cm, girada a cada pedido. |
| foto curvada de cima para baixo | A foto enrolada de cima para baixo, com raio de 4 a 10 cm, girada a cada pedido. |
| foto curvada na diagonal | A foto enrolada com o eixo a 45°, com raio de 3 a 8 cm, girada a cada pedido. |
| foto dobrada na linha do nariz | A foto dobrada em V pela linha do nariz, de 25° a 50° de cada lado, girada a cada pedido. |
| foto dobrada na altura do nariz | A foto dobrada em V de lado a lado, na altura do nariz, de 25° a 50° de cada lado, girada a cada pedido. |
| troca de fotos | Fotos da mesma pessoa em poses diferentes, alinhadas, trocadas a cada pedido pela que mais se parece com o movimento. |

## O critério

Para APCER de até **1%** na pior espécie, o relevo mínimo é **0,157** meia distância entre os olhos — uns 4,9 mm num rosto de olhos a 63 mm.

| Espécie | Fizeram os movimentos | Maior relevo | APCER | Intervalo de 95% |
|---|--:|--:|--:|---|
| foto parada | 0 de 300 | — | 0,00% | 0,00% a 1,26% |
| foto plana inclinada | 40 de 300 | 0,020 | 0,00% | 0,00% a 1,26% |
| foto curvada de lado | 21 de 300 | 0,027 | 0,00% | 0,00% a 1,26% |
| foto curvada de cima para baixo | 137 de 300 | 0,044 | 0,00% | 0,00% a 1,26% |
| foto curvada na diagonal | 74 de 300 | 0,236 | 1,00% | 0,34% a 2,90% |
| foto dobrada na linha do nariz | 26 de 300 | 0,022 | 0,00% | 0,00% a 1,26% |
| foto dobrada na altura do nariz | 156 de 300 | 0,124 | 0,00% | 0,00% a 1,26% |
| troca de fotos | 0 de 300 | — | 0,00% | 0,00% a 1,26% |

A pior espécie é a **foto curvada na diagonal**, com APCER de 1,00% (3 de 300), e é ela que decide o critério. A maior parte das tentativas dela termina antes: o relevo só é julgado quando a foto consegue fazer os três movimentos sem perder o acompanhamento.

Nas 7 espécies sem nenhum ataque aceito, zero em 300 não quer dizer zero: o APCER verdadeiro ainda pode chegar a 1,26%, o limite de cima do intervalo. É o que 300 apresentações permitem afirmar.

## Onde cada ataque parou

| Espécie | Tempo esgotado | Lado errado | Rosto perdido | Câmera falhou | Sem relevo | Aceitos |
|---|--:|--:|--:|--:|--:|--:|
| foto parada | 297 | 0 | 3 | 0 | 0 | 0 |
| foto plana inclinada | 246 | 2 | 12 | 0 | 40 | 0 |
| foto curvada de lado | 69 | 0 | 210 | 0 | 21 | 0 |
| foto curvada de cima para baixo | 83 | 0 | 80 | 0 | 137 | 0 |
| foto curvada na diagonal | 43 | 14 | 169 | 0 | 71 | 3 |
| foto dobrada na linha do nariz | 0 | 0 | 274 | 0 | 26 | 0 |
| foto dobrada na altura do nariz | 1 | 0 | 143 | 0 | 156 | 0 |
| troca de fotos | 0 | 0 | 300 | 0 | 0 | 0 |

## O vídeo gravado

Não foi simulado: a gravação de uma pessoa de verdade tem relevo, e a cena só sabe mostrar fotos. O que derruba a gravação é o sorteio. Uma gravação com três movimentos numa ordem acerta 1 dos 56 desafios: 1,79% por tentativa. Limitar as tentativas de cada trabalhador fica com a API, na Parte 6.

## As apresentações genuínas

Nenhuma. **A falsa rejeição (BPCER) não foi medida.** A medida pede gente de verdade fazendo o desafio diante de uma câmera, e as bases públicas de vídeo com o rosto virando são de uso só em pesquisa ou não têm o movimento. O critério acima saiu só do lado do ataque. A ferramenta mede o BPCER com sessões gravadas (`--genuinas`), e a medida com o pessoal da empresa fica para a Parte 8 (ADR-0008).

## O que o ensaio não mede

- **A luz.** A geometria da cena é exata — a perspectiva, a curva, a dobra —, mas a aparência é a da foto: sem o brilho do papel e da tela, sem o moiré, sem o borrão do movimento.
- **Máscaras e rostos injetados.** Uma máscara rígida tem relevo, e um vídeo feito por computador entregue no lugar da câmera também; a prova pelo relevo não os pega. Contra eles servem uma câmera com profundidade ou infravermelho e um coletor que não aceite imagem de fora.
- **O relevo de gente de verdade**, que muda de rosto para rosto.

## Como reproduzir

```powershell
dotnet run --project tools/PontoFacial.EnsaioDaProvaDeVida -c Release -- <pasta da base> docs/ensaio/prova-de-vida-lfw.md
```

Cada apresentação é sorteada com semente fixa: a mesma base dá os mesmos números.
