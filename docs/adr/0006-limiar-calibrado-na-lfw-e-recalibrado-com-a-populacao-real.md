# ADR-0006 — O limiar de partida vem da LFW, e se recalibra com a população real

**Status:** aceita

## Contexto

A ADR-0004 decidiu que o limiar sai da falsa aceitação tolerada, medida com comparações suficientes. Falta saber medida onde. Um sistema de ponto novo não tem fotos do próprio pessoal, e a calibração precisa de milhares de pessoas para medir falsa aceitação de 1:10.000 com confiança.

A **LFW** (*Labeled Faces in the Wild*) é a base pública mais usada para isso: 13.233 fotos de 5.749 pessoas, tiradas da imprensa. É também a base em que o OpenCV publicou o limiar do SFace. Os próprios autores avisam que ela representa mal muitos grupos: há poucas crianças, poucas pessoas acima de 80 anos, proporção pequena de mulheres, e muitas etnias com pouca ou nenhuma presença. As fotos são de 250×250 pixels, de pessoas famosas, com a luz da imprensa, longe da luz da portaria de uma obra às seis da manhã.

## Decisão

1. **A ferramenta `tools/PontoFacial.Calibracao` compara todas as fotos com todas** e calibra pelo domínio (`CalibracaoDoLimiar`). Na LFW inteira:
   - o rosto foi extraído de 12.983 fotos (98,1%);
   - foram 230.802 comparações da mesma pessoa e 84.041.851 entre pessoas diferentes;
   - o processo levou um minuto e meio.
2. **O limiar de partida é 0,4317**, o de falsa aceitação de 1:10.000. A falsa rejeição nesse limiar é de 0,83%, com intervalo de 95% de 0,79% a 0,87%. Ele fica em `LimiaresCalibrados.SFaceNaLfw`, com o relatório em `docs/calibracao/lfw-sface-2021dec.md`.
3. **A ponta da curva não vale como medida do reconhecedor.** Os pares de pessoas diferentes mais parecidos da LFW são erros da base:
   - a mesma foto em duas pastas;
   - a mesma pessoa com o nome escrito de dois jeitos;
   - fotos com duas pessoas.

   Eles sustentam a falsa aceitação medida abaixo de 1:1.000.000. A ferramenta lista esses pares na tela para quem calibra conferir, e não põe os nomes no relatório.
4. **A empresa recalibra com o próprio pessoal.** A ferramenta aceita qualquer base organizada em uma pasta por pessoa, e o limiar novo diz a origem. O erro por grupo — gênero, idade, cor da pele — é medido numa parte própria do roteiro, antes de o sistema ir para uma empresa de verdade.

## Consequências

- O limiar tem número, origem e intervalo de confiança; não é um valor copiado sem saber de onde veio. Na LFW, o ponto de 1:1.000 dá 0,3655, perto do 0,363 que o OpenCV publicou, o que confirma o pré-processamento.
- Com falsa rejeição de 0,83%, uma empresa de cem pessoas que marcam quatro vezes por dia tem perto de três recusas indevidas por dia. Cada uma se resolve tentando de novo ou pelo código pessoal, e nenhuma vira falta, porque o código pessoal está sempre disponível (ADR-0002).
- Um limiar calibrado na LFW pode errar mais com quem a LFW representa mal. Até a medição por grupo, isso é um risco conhecido e registrado, não um detalhe.
- As fotos da LFW não estão no repositório, só o relatório. Quem quiser reproduzir baixa a base e roda a ferramenta; duas execuções seguidas deram os mesmos números.
