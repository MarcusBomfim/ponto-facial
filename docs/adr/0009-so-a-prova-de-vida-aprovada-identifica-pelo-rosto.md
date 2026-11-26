# ADR-0009 — Só a prova de vida aprovada identifica pelo rosto

**Status:** aceita

## Contexto

Até a Parte 2, a marcação pelo rosto pedia uma `ConferenciaFacial` que alcançasse o limiar. A conferência comparava um vetor solto com o cadastro, e um vetor tirado de uma foto do trabalhador confere tanto quanto um tirado do rosto dele. A prova de vida (ADR-0007) só protege a marcação se for obrigatória para ela, e se o rosto comparado for o mesmo que fez os movimentos.

## Decisão

1. **A conferência sai da prova de vida.** `CadastroFacial.Conferir(prova, limiar)` só aceita prova aprovada. Ela compara com o cadastro os dois vetores da prova — o do começo, de frente, e o do fim, de volta à frente — e fica com a menor similaridade: os dois têm de ser do trabalhador. O instante da conferência é o do fim da prova.
2. **A identificação pelo rosto exige a prova.** `IdentificacaoDoTrabalhador.PorRosto` recusa a conferência que não saiu de uma prova aprovada, com a frase: "uma foto dele conferiria do mesmo jeito".
3. **O vetor solto continua comparável.** A calibração do limiar compara vetores de fotos, e `Conferir(modelo, limiar, em)` continua para ela, mas a conferência que ele dá não identifica ninguém na marcação.
4. **O critério de partida vem do ensaio.** `LimiaresCalibrados.ProvaDeVidaNaLfw` é o relevo mínimo de **0,157**, com APCER de até 1% na pior espécie de ataque. O relatório está em `docs/ensaio/prova-de-vida-lfw.md`.

## Consequências

- Não há caminho, no domínio, do vetor de uma foto até uma marcação pelo rosto. A ordem das conferências é: autorização vigente, cadastro do mesmo trabalhador, prova de vida, limiar.
- O rosto que entra na conferência é o que fez os movimentos. Entre o começo e o fim, quem liga um ao outro é o acompanhamento, que se perde numa troca. A conferência dos dois vetores é a segunda trava.
- **A foto enrolada na diagonal é o ponto fraco medido.** Com o eixo inclinado, nenhuma das duas vizinhanças do nariz fica numa reta do papel, e um pouco da curva aparece nos dois relevos. No critério, 3 das 300 passaram — o 1% tolerado —, e o maior relevo delas foi 0,236. Medir o relevo também nas diagonais fecharia essa brecha. Fica para quando houver sessões genuínas que mostrem quanto isso custa em falsa rejeição.
- O critério de partida só sabe o lado do ataque: a falsa rejeição com gente de verdade não foi medida (ADR-0008). A empresa que adotar o sistema mede com as próprias sessões, antes de depender da biometria. Até lá, quem for recusado tenta de novo ou marca pelo código pessoal, que vale o mesmo (ADR-0002).
