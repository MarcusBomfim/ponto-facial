# Calibração do limiar: sface-2021dec na lfw

Gerado por `tools/PontoFacial.Calibracao` em 2026-11-24, em 1,5 minutos. As fotos não estão no repositório; só este relatório.

## A base

- 13.233 imagens de 5.749 pessoas, uma pasta por pessoa.
- Rosto extraído de 12.983 imagens (98,11%). Quando a foto tem mais de um rosto, vale o mais perto do centro.
- Sem vetor por nenhum rosto achado: 106.
- Sem vetor por rosto pequeno demais: 143.
- Sem vetor por rosto cortado na borda: 1.
- Comparações da mesma pessoa (genuínas): 230.802.
- Comparações entre pessoas diferentes (impostoras): 84.041.851.

## O limiar

Para falsa aceitação de até **1:10.000**, o limiar é **0,4317**.

| | Medida | Erros | Intervalo de 95% |
|---|--:|--:|---|
| Falsa aceitação | 0,0100% | 8.388 em 84.041.851 | 0,0098% a 0,0102% |
| Falsa rejeição | 0,83% | 1.910 em 230.802 | 0,79% a 0,87% |

A taxa de erro igual (EER) é 0,32%, no limiar 0,3316.

## A curva

| Falsa aceitação tolerada | Limiar | Falsa aceitação medida | Falsa rejeição |
|---|--:|--:|--:|
| 1:10 | 0,2029 | 9,98% | 0,25% |
| 1:100 | 0,2959 | 1,00% | 0,28% |
| 1:1.000 | 0,3655 | 0,100% | 0,39% |
| 1:10.000 | 0,4317 | 0,0100% | 0,83% |
| 1:100.000 | 0,5769 | 0,00100% | 9,38% |
| 1:1.000.000 | 0,7104 | 0,000100% | 57,43% |
| 1:10.000.000 | 0,9102 | 0,0000095% | 99,97% |

Na ponta da curva, a falsa aceitação medida vem em boa parte de erros da base, e não do reconhecedor: há 110 pares de pessoas diferentes com similaridade de 0,7 ou mais, e os mais parecidos são a mesma foto em duas pastas, a mesma pessoa com o nome escrito de dois jeitos, ou fotos com duas pessoas em que o rosto tomado não é o de quem dá nome à pasta. A ferramenta lista esses pares na tela, para quem calibra conferir.

## Como reproduzir

```powershell
dotnet run --project tools/PontoFacial.Calibracao -c Release -- <pasta da base> docs/calibracao/lfw-sface-2021dec.md
```

As similaridades são contadas em faixas de 0,0001, e o limiar é a borda de uma faixa (ADR-0004).
