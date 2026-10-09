# Recuperador Firebird

Programa para Windows que tenta recuperar bancos Firebird em um único arquivo `.fdb` ou `.gdb`. O banco original é copiado antes de qualquer tentativa de reparo. O programa gera um backup com o `gbak`, restaura esse backup em outro arquivo e confere o resultado com o `gfix`.

Se o backup inicial falhar, o programa pergunta antes de reparar uma cópia. O reparo pode descartar dados danificados.

## Como usar

1. Abra `RecuperadorFirebird.exe` no computador onde o Firebird está instalado.
2. Escolha o banco e a pasta onde os resultados serão salvos.
3. Confira se a pasta `bin` selecionada pertence à versão do Firebird usada pelo banco.
4. Informe o usuário e a senha do Firebird.
5. Feche o sistema que usa o banco e desconecte os usuários. Depois, inicie a recuperação.

O programa cria uma pasta `RECUPERACAO_...` dentro do destino escolhido. Ao terminar, abra `LEIA_O_RESULTADO.txt`. Esse arquivo explica as etapas realizadas, os erros encontrados e se houve reparo. Os arquivos `.log` guardam as mensagens completas das ferramentas Firebird.

## Arquivos gerados

- `*_ORIGINAL_COPIA.fdb` ou `.gdb`: cópia do banco antigo, feita antes do reparo.
- `*_TRABALHO_REPARO.fdb` ou `.gdb`: cópia usada no reparo, quando ele é necessário e autorizado.
- `*_BACKUP.fbk` ou `*_BACKUP_APOS_REPARO.fbk`: backup gerado pelo Firebird.
- `*_RECUPERADO.fdb`: banco novo para testar no sistema.
- `LEIA_O_RESULTADO.txt`: resumo em português do que aconteceu.
- `*.log`: detalhes técnicos de cada etapa.

O relatório pode mostrar a quantidade de erros informada pelo Firebird, mas não consegue apontar quais registros foram perdidos quando a ferramenta não fornece essa informação. Antes de usar o banco novo, confira cadastros, movimentações, relatórios e dados recentes. O programa não substitui o banco original automaticamente.

## Compilar

É preciso ter Windows, .NET Framework 4.x e uma instalação do Firebird com `gbak.exe` e `gfix.exe`. Para recompilar o programa, execute `COMPILAR.bat`. O código está em `FirebirdRecuperador.cs` e `RelatorioLegivel.cs`.

Os dois arquivos `.bat` de recuperação foram mantidos porque serviram de base para o aplicativo. Para usar a interface e o relatório em português, abra o `.exe`.

Este repositório é público. Bancos reais, backups e logs de clientes não devem ser enviados para cá; esses arquivos estão bloqueados pelo `.gitignore`.
