# ADR-0005 — O rosto com YuNet e SFace no ONNX Runtime, em código gerenciado

**Status:** aceita

## Contexto

Reconhecer um rosto tem quatro passos: ler a imagem, achar o rosto e os pontos dele, alinhá-lo e transformá-lo num vetor. Cada passo pede uma escolha de modelo ou de biblioteca, e duas restrições valem para todas:

- **A licença tem de permitir uso comercial.** Os modelos pré-treinados do InsightFace, os mais citados, são liberados só para pesquisa não comercial. Uma empresa que usasse este sistema com eles estaria fora da licença.
- **O sistema roda em .NET**, em servidor Linux, em contêiner e no CI, sem depender de bibliotecas instaladas na máquina.

O OpenCV Zoo publica dois modelos pequenos, feitos para funcionar juntos: o **YuNet** (licença MIT), que acha o rosto e cinco pontos dele, e o **SFace** (licença Apache 2.0), que transforma o rosto alinhado em 128 números. O OpenCV os usa pelo `FaceDetectorYN` e pelo `FaceRecognizerSF`, mas levar o OpenCV inteiro, nativo, para fazer só isso é muito peso.

## Decisão

1. **Os dois modelos rodam no ONNX Runtime** (licença MIT), e o que o OpenCV faz em volta deles é reproduzido em C#, conferido contra o código-fonte do OpenCV:
   - **Detecção:** a imagem entra em azul, verde e vermelho, de 0 a 255, completada com preto até múltiplos de 32. Cada célula da grade, nos passos 8, 16 e 32, dá uma confiança igual à média geométrica das notas de classe e de objeto, a caixa e os cinco pontos. Caixas que se sobrepõem mais de 30% são suprimidas, e fica a mais confiante. A confiança mínima é 0,9.
   - **Alinhamento:** a semelhança (giro, escala e deslocamento) que leva os cinco pontos ao molde de 112×112 do ArcFace, pelo menor erro quadrático, e o recorte por interpolação bilinear.
   - **Vetor:** o recorte entra no SFace em vermelho, verde e azul, de 0 a 255; os 128 números saem e o domínio os guarda com norma 1.
2. **A imagem é lida pelo SkiaSharp** (licença MIT), com a orientação que a câmera gravou no EXIF aplicada: foto de celular tirada de lado chega deitada, e o detector não acha rosto deitado. O ImageSharp ficou de fora porque, desde a versão 3, tem a licença dividida da Six Labors, que pede licença comercial em parte dos usos. O `System.Drawing` só funciona no Windows.
3. **A detecção roda numa cópia com o lado maior em 320 pixels; o alinhamento, na imagem inteira.** O YuNet acha rostos de uns 10 a 300 pixels, e o rosto de uma selfie em resolução cheia passa disso. O recorte de 112×112 sai da imagem original, com todos os pixels que ela tem.
4. **A captura só vale com um rosto, perto e inteiro.** Mais de um rosto, nenhum rosto, olhos a menos de 30 pixels um do outro, ou algum dos cinco pontos fora da imagem: a captura é recusada, com a frase para quem está diante da câmera. Para bases de imagens como a LFW, que trazem gente ao fundo, há a escolha do rosto mais perto do centro.
5. **Os modelos não entram no repositório.** O build baixa cada arquivo do commit fixado do OpenCV Zoo e confere o SHA-256 antes de compilar; um arquivo trocado no caminho para o build. O CI guarda os modelos em cache pela versão do projeto que os fixa.
6. **A foto não fica.** A imagem vive só em memória, enquanto o vetor é extraído. O que sai do reconhecedor é o `ModeloFacial` e a posição do rosto.

## Consequências

- O reconhecimento roda em qualquer lugar onde o .NET roda, sem OpenCV instalado. O ONNX Runtime e o SkiaSharp trazem as bibliotecas nativas nos pacotes, inclusive a de Linux sem dependências.
- Reproduzir o pré e o pós-processamento é responsabilidade nossa. Uma diferença sutil, como canal trocado, recorte deslocado ou sem normalização, não quebra nada visível: só piora o reconhecimento. Por isso o alinhamento e a decodificação têm testes de matemática, e o pipeline tem testes com fotos reais.
- A primeira compilação baixa 37 MB. Quem compila sem internet precisa pôr os dois arquivos em `modelos/` antes; o SHA-256 é conferido do mesmo jeito.
- Trocar de modelo muda o nome do reconhecedor gravado em cada vetor. Os cadastros antigos não se comparam com os novos (ADR-0004), e a troca pede um recadastramento.
