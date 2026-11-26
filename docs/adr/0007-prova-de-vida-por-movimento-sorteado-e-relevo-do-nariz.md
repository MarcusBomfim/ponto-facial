# ADR-0007 — A prova de vida pede movimentos sorteados e mede o relevo do nariz

**Status:** aceita

## Contexto

O reconhecimento da Parte 2 compara vetores. A foto do trabalhador dá o mesmo vetor que o rosto dele: sem prova de vida, quem tem uma foto do colega — no celular, impressa — marca o ponto por ele. A ISO/IEC 30107-1 chama isso de ataque de apresentação, e a coisa posta diante da câmera de instrumento de ataque.

Há três caminhos conhecidos:

- **Um modelo treinado** para distinguir foto de rosto numa imagem só, como o MiniFASNet. Ele aprende o brilho do papel, o moiré da tela. Para medir e calibrar, precisa de uma base de ataques. As públicas — CASIA-FASD, Replay-Attack, OULU-NPU, SiW, CelebA-Spoof — são só para pesquisa, e a maioria pede termo assinado por uma instituição. Um modelo que não se mede não se calibra (ADR-0004).
- **Uma câmera com profundidade ou infravermelho.** Resolve boa parte do problema no hardware, mas o coletor deste projeto é o celular ou o tablet comum da portaria.
- **A prova ativa:** pedir movimentos e medir a geometria do que se mexe.

O primeiro protótipo da prova ativa mediu o relevo pelos cinco pontos que o YuNet acha a cada quadro. Foram 60 fotos de frente da LFW giradas numa câmera simulada. Numa foto inclinada, os pontos do detector erraram de 5 a 20 pixels, com 87 pixels entre os olhos: treinado em rostos, ele põe o nariz onde um rosto teria nariz. Uma foto plana aparecia com relevo em torno de 0,45. Pares de fotos da mesma pessoa na LFW deram 0,95 de mediana, mas com 5% abaixo de 0,52. Com essa sobreposição, nenhum critério separa uma coisa da outra.

## Decisão

1. **Um desafio sorteado.** São três movimentos entre quatro — esquerda, direita, cima, baixo —, com pelo menos um para o lado, o que dá 56 desafios. O sorteio usa o gerador criptográfico. Uma gravação feita numa ordem só acerta 1 desafio em 56 por tentativa.
2. **Os pontos são acompanhados, e não detectados de novo.** O detector acha o rosto no quadro de referência, e daí em diante o fluxo óptico de Lucas e Kanade segue nove pontos:
   - os cinco do detector: olhos, nariz e cantos da boca;
   - a raiz do nariz e o centro da boca;
   - as duas bochechas, na altura do nariz.

   Cada ponto é também acompanhado de volta (Kalal et al., 2010). Um ponto que não volta ao lugar perde o acompanhamento inteiro.
3. **O relevo pela homografia do plano.** Os olhos e os cantos da boca definem um plano. A homografia que os leva da referência ao quadro atual é o movimento exato de uma foto ou de uma tela, com perspectiva e tudo. O quanto cada outro ponto se desvia dela, dividido pela inclinação do plano, é a profundidade dele. Daí saem dois relevos:
   - o **lateral**, que compara o nariz com as bochechas;
   - o **vertical**, que compara o nariz com a reta entre a raiz do nariz e o centro da boca.

   O relevo do quadro é o menor dos dois.
4. **Por que o menor.** Papel e tela são superfícies desenvolvíveis: por cada ponto passa uma reta que fica na superfície. Curvado de lado a lado, o papel dá relevo lateral e nenhum vertical; curvado de cima a baixo, o contrário; dobrado, também. O nariz de uma pessoa sobressai nas duas direções.
5. **A sessão** começa com o rosto de frente e o vetor dele. Cada movimento tem três tempos: ir para o lado pedido, ficar 0,3 s virado, voltar. A sessão recusa sem esperar o fim quando:
   - o movimento não sai em 6 s;
   - o rosto vai para o lado errado;
   - o acompanhamento se perde;
   - a câmera deixa passar mais de 0,25 s entre dois quadros.

   No fim, o rosto de frente dá o vetor de novo. O relevo de cada movimento é a mediana dos quadros no auge; o da prova, o menor entre os movimentos.
6. **A recusa por relevo diz uma frase genérica.** Ela não aponta o que faltou, para quem testa um ataque não aprender com a resposta o que ajustar. As outras recusas — tempo, lado errado, rosto perdido — dizem o que fazer.
7. **O critério tem origem.** O relevo mínimo sai de um ensaio (ADR-0008) e diz de qual, como o limiar do rosto.

## Consequências

- Os ataques com foto, impressa ou na tela, caem por um destes caminhos:
  - a foto parada nunca faz o movimento;
  - a foto plana inclinada até finge o giro, porque na diagonal ela cisalha, mas não tem relevo;
  - a curvada e a dobrada só têm relevo numa direção, e as muito curvadas perdem o acompanhamento antes de o nariz andar o bastante;
  - a troca de fotos não atravessa o acompanhamento;
  - a gravação numa ordem só erra o sorteio.
- **Uma máscara rígida e um rosto sintético entregue no lugar da câmera têm relevo**, e esta prova não os pega. Contra eles servem a câmera com profundidade ou infravermelho e um coletor que não aceite imagem de fora (Parte 7). O limite fica registrado.
- A prova leva de 5 a 10 segundos e pede que a pessoa mexa a cabeça. Quem não pode, ou não quer, marca pelo código pessoal, que vale o mesmo (ADR-0002).
- O relevo depende do rosto: um nariz pouco saliente tem menos. A medida por grupo, na Parte 8, vai dizer se o critério recusa mais uns que outros.
