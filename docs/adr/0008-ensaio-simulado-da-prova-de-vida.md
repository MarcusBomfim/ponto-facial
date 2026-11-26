# ADR-0008 — O ataque à prova de vida é medido numa câmera simulada, e a falsa rejeição, com gente

**Status:** aceita

## Contexto

A ISO/IEC 30107-3 manda medir a prova de vida por espécie de instrumento de ataque:

- o **APCER** de cada espécie: a fração dos ataques dela que a prova aceitou;
- o **APCER da pior espécie**, que é o do sistema, e não a média entre elas;
- o **BPCER**: a fração das pessoas de verdade que ela recusou.

Um ensaio de verdade usa fotos impressas, telas e voluntários diante da câmera, num laboratório. Este projeto não tem laboratório, e as bases prontas não servem:

- As bases públicas de ataques são só para pesquisa (ADR-0007).
- As bases de vídeo com o rosto virando também são só de pesquisa.
- Os vídeos de entrevista da NASA, em domínio público, foram baixados e varridos: em 15 e 20 minutos de duas entrevistas, nenhuma janela de 2 segundos com a cabeça girando o bastante.
- A LFW tem fotos soltas, e não sequências; o acompanhamento não se mede nela.

## Decisão

1. **O lado do ataque é medido numa câmera simulada, com geometria exata.**
   - A câmera tem 640×480 pixels e 60° de abertura, como uma webcam.
   - A foto é impressa em tamanho real, com os olhos a 63 mm, e posta na forma da espécie: plana, enrolada num cilindro de eixo de pé, deitado ou na diagonal, ou dobrada em V.
   - Ela é girada a 35–45 cm da câmera, com o tremor da mão e o ruído do sensor.
   - A imagem é feita raio a raio: o pixel recebe a cor do ponto da foto que o raio encontra.
   - O detector, o acompanhamento e a sessão são os de verdade.
2. **O atacante faz o melhor que a espécie permite.** Ele olha a tela a cada quadro e insiste no movimento pedido até a instrução mudar. Com a foto plana, inclina na diagonal, que finge o giro. Com a curvada e a dobrada, vira mais que uma pessoa viraria. Na troca de fotos, mostra a foto da mesma pessoa que mais parece feita no movimento pedido.
3. **Oito espécies, 300 apresentações cada**, com as fotos de frente da LFW e sementes fixas. Pela regra de três, 300 sem erro dizem que o APCER verdadeiro fica abaixo de 1% com 95% de confiança.
4. **O critério sai do ataque.** O relevo mínimo é o menor que deixa o APCER da pior espécie em até 1% (`EnsaioDaProvaDeVida.Calibrar`).
5. **O BPCER fica como não medido, e o relatório diz isso.** A ferramenta mede o BPCER com sessões gravadas por gente de verdade (`--genuinas`): uma pasta por sessão, com os quadros e o desafio que a pessoa seguiu. A medida com o pessoal de uma empresa é da Parte 8.

## Consequências

- A geometria de cada ataque é a de verdade, mas a aparência é a da foto. A cena não imita o brilho do papel, o moiré da tela nem o borrão do movimento, que podem atrapalhar o acompanhamento — em geral contra o atacante.
- Máscaras rígidas e rostos sintéticos injetados ficam fora do ensaio, como ficam fora da prova (ADR-0007).
- O critério sabe quantos ataques deixa passar, mas não quantas pessoas recusa. Se, com gente de verdade, a recusa for alta, o critério se recalibra com as sessões gravadas. Até lá, o código pessoal está sempre disponível (ADR-0002).
- O ensaio leva uns 20 minutos e se refaz com o comando do relatório.
