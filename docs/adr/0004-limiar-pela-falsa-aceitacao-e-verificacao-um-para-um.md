# ADR-0004 — O limiar sai da falsa aceitação tolerada, e o rosto confirma quem o trabalhador diz ser

**Status:** aceita

## Contexto

O reconhecimento facial não responde "é ou não é". Ele compara dois vetores e devolve uma similaridade; quem decide é o limiar. Todo limiar erra de dois jeitos:

- **Falsa aceitação** (FMR, na ISO/IEC 19795-1): aceitar o rosto de outra pessoa. No ponto, é o colega que marca pelo outro.
- **Falsa rejeição** (FNMR): recusar o rosto da pessoa certa. No ponto, é o trabalhador que tenta três vezes na portaria, com a fila atrás.

Subir o limiar diminui um erro e aumenta o outro. Um limiar copiado de um artigo, sem dizer com que base foi medido, esconde essa troca.

A comparação pode ser **um para um** — o trabalhador diz quem é, e o rosto confirma — ou **um para muitos** — o sistema procura o rosto entre todos os cadastrados. No um para muitos, cada busca compara com N cadastros, e a chance de alguma falsa aceitação cresce com N: com falsa aceitação de 1:10.000 por comparação e 500 trabalhadores, quase 5% das buscas feitas por um rosto desconhecido acham alguém.

## Decisão

1. **Escolhe-se a falsa aceitação tolerada, e o limiar sai dela.** O limiar é o menor que mantém a falsa aceitação medida dentro do alvo. A falsa rejeição nesse limiar é medida e informada, mas não é o critério. Para o ponto, o alvo padrão é **1:10.000**.
2. **A taxa só vale com comparações suficientes.** Pela regra de três, N comparações sem erro ainda deixam o erro verdadeiro chegar a uns 3/N com 95% de confiança. A calibração recusa um alvo menor que 3 sobre o número de comparações impostoras, e cada taxa sai com o intervalo de 95% de Wilson.
3. **As similaridades são contadas em faixas de 0,0001.** Uma base como a LFW dá dezenas de milhões de comparações. Com a contagem por faixa, a taxa em cada borda é exata e a memória é fixa. O limiar é uma das bordas.
4. **O limiar diz de onde veio.** `LimiarDeAceitacao` guarda o reconhecedor e a origem — a base, o alvo, a data —, e não se compara um vetor com o limiar de outro reconhecedor.
5. **Verificação um para um.** O trabalhador se identifica — pelo CPF no tablet, pelo crachá, pela conta no celular — e o rosto confirma que é ele. A falsa aceitação por marcação é a da comparação, e não cresce com o tamanho da empresa.
6. **O cadastro tem pelo menos três capturas, e elas têm de conferir entre si** com o mesmo limiar da marcação. O modelo guardado é a média delas, com norma 1. Uma captura de outro rosto recusa o cadastro inteiro, em vez de entrar na média.
7. **O domínio não guarda imagem.** O rosto entra como `ModeloFacial`, um vetor com norma 1. Um teste de arquitetura procura, em todos os tipos do domínio, campos ou propriedades de bytes e de fluxo, que é onde uma foto caberia.

## Consequências

- Mudar o alvo é decisão de negócio, registrada, e não ajuste de código: o mesmo modelo, com outra tolerância, dá outro limiar e outra falsa rejeição.
- O trabalhador precisa dizer quem é antes de olhar para a câmera. É um passo a mais na portaria; em troca, a segurança não piora quando a empresa cresce.
- O limiar calibrado numa base pública não é o da população de uma obra. A calibração é uma ferramenta, para rodar de novo com as capturas reais, e o erro por grupo é medido numa parte própria do roteiro.
- O vetor não é a foto, mas também não é anônimo. Há técnicas que reconstroem um rosto aproximado a partir dele, e por isso ele é dado biométrico: guardado cifrado e apagado quando a autorização acaba (ADR-0002).
