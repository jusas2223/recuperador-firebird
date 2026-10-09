@echo off
setlocal EnableExtensions DisableDelayedExpansion
title Recuperacao de bancos - Firebird 2.5
rem Baseado nos comandos da imagem fornecida pelo usuario.
rem Referencias: https://www.firebirdsql.org/file/documentation/html/en/firebirddocs/gfix/firebird-gfix.html
rem https://www.firebirdsql.org/file/documentation/html/en/firebirddocs/gbak/firebird-gbak.html
rem ISC_USER e ISC_PASSWORD substituem -user e -pass nas ferramentas Firebird.
rem As credenciais duram apenas esta sessao e nao sao gravadas neste arquivo.

echo ============================================================
echo       RECUPERACAO DE BANCOS - FIREBIRD 2.5
echo ============================================================
echo Execute no servidor do banco, como administrador.
echo Use ferramentas da versao compativel com o seu banco.
echo Antes de reparar, preserve uma copia de seguranca consistente.
echo Uma copia fisica do .FDB deve ser feita com o banco sem uso.
echo Feche os sistemas e desconecte os usuarios durante a manutencao.
echo Este menu executa uma operacao por vez.
echo.

set "FB_BIN="
if exist "%ProgramFiles%\Firebird\Firebird_2_5\bin\gfix.exe" set "FB_BIN=%ProgramFiles%\Firebird\Firebird_2_5\bin"
if not defined FB_BIN if exist "%ProgramFiles(x86)%\Firebird\Firebird_2_5\bin\gfix.exe" set "FB_BIN=%ProgramFiles(x86)%\Firebird\Firebird_2_5\bin"
if defined FB_BIN goto banco

:pasta
echo Informe a pasta bin que contem gfix.exe e gbak.exe.
set "FB_BIN="
set /p "FB_BIN=Pasta: "
if not defined FB_BIN goto pasta
set "FB_BIN=%FB_BIN:"=%"
if not exist "%FB_BIN%\gfix.exe" goto pasta_invalida
if not exist "%FB_BIN%\gbak.exe" goto pasta_invalida
goto banco

:pasta_invalida
echo ERRO: gfix.exe ou gbak.exe nao encontrado nessa pasta.
goto pasta

:banco
if not exist "%FB_BIN%\gbak.exe" goto pasta
set "DB="
echo.
set /p "DB=Caminho completo do arquivo do banco .FDB: "
if not defined DB goto banco
set "DB=%DB:"=%"
if exist "%DB%" goto credenciais
echo ERRO: arquivo nao encontrado. Informe um caminho local neste servidor.
goto banco

:credenciais
set "ISC_USER=SYSDBA"
set /p "ISC_USER=Usuario [SYSDBA]: "
:senha
set "ISC_PASSWORD="
echo A senha digitada ficara visivel nesta janela.
set /p "ISC_PASSWORD=Senha do Firebird: "
if not defined ISC_PASSWORD goto senha
cls

:menu
echo.
echo ============================================================
echo Banco: "%DB%"
echo Ferramentas: "%FB_BIN%"
echo ============================================================
echo 1 - Verificar a base: gfix -validate -full
echo 2 - Reparar a base: gfix -mend -ignore
echo 3 - Gerar backup de recuperacao .FBK
echo 4 - Restaurar .FBK em um NOVO banco e verificar
echo 5 - Cancelar todas as transacoes EM LIMBO
echo 6 - Colocar o banco em shutdown imediato
echo 7 - Colocar o banco online
echo 8 - Executar sweep / garbage collection
echo 9 - Verificar sem modificar: gfix -validate -full -no_update
echo 0 - Sair
echo.
echo Sequencia da imagem: 1, 2 se necessario, 1 novamente, 3 e 4.
echo Leia as mensagens das ferramentas antes de escolher o proximo passo.
choice /c 1234567890 /n /m "Escolha uma opcao: "
if errorlevel 10 goto fim
if errorlevel 9 goto verificar_leitura
if errorlevel 8 goto sweep
if errorlevel 7 goto online
if errorlevel 6 goto shutdown
if errorlevel 5 goto rollback
if errorlevel 4 goto restaurar
if errorlevel 3 goto backup
if errorlevel 2 goto reparar
if errorlevel 1 goto verificar
goto fim

:verificar
echo A validacao da imagem pode atualizar estruturas do banco.
echo A opcao 9 permite apenas diagnosticar, sem atualizar.
choice /c SN /n /m "Copia preservada e banco sem usuarios? Continuar [S/N]: "
if errorlevel 2 goto menu
"%FB_BIN%\gfix.exe" -validate -full "%DB%"
goto resultado

:verificar_leitura
"%FB_BIN%\gfix.exe" -validate -full -no_update "%DB%"
goto resultado

:reparar
echo ATENCAO: o reparo pode descartar registros corrompidos.
choice /c SN /n /m "Copia preservada e banco sem usuarios? Executar reparo [S/N]: "
if errorlevel 2 goto menu
"%FB_BIN%\gfix.exe" -mend -ignore "%DB%"
set "RC=%errorlevel%"
echo Codigo de saida do reparo: %RC%
echo Leia o resultado acima e execute a opcao 1 novamente.
goto pausa

:backup
set "FBK="
echo -g desativa garbage collection; -limbo ignora transacoes em limbo.
echo O backup de recuperacao pode omitir dados de transacoes em limbo.
echo Use um arquivo .FBK novo, em uma pasta existente.
set /p "FBK=Caminho completo do backup .FBK [Enter cancela]: "
if not defined FBK goto menu
set "FBK=%FBK:"=%"
if exist "%FBK%" goto destino_existente
"%FB_BIN%\gbak.exe" -g -b -z -limbo -v "%DB%" "%FBK%"
set "RC=%errorlevel%"
echo Codigo de saida do backup: %RC%
if not "%RC%"=="0" echo Backup falhou: nao utilize um arquivo parcial na restauracao.
if "%RC%"=="0" echo Backup concluido. Use a opcao 4 para restaurar em um novo banco.
goto pausa

:restaurar
set "FBK="
set /p "FBK=Caminho completo do backup .FBK [Enter cancela]: "
if not defined FBK goto menu
set "FBK=%FBK:"=%"
if not exist "%FBK%" goto backup_ausente
set "DB_NOVO="
echo Informe um novo arquivo .FDB, em uma pasta existente.
set /p "DB_NOVO=Caminho completo do NOVO banco [Enter cancela]: "
if not defined DB_NOVO goto menu
set "DB_NOVO=%DB_NOVO:"=%"
if exist "%DB_NOVO%" goto destino_existente
"%FB_BIN%\gbak.exe" -g -c -z -v "%FBK%" "%DB_NOVO%"
set "RC=%errorlevel%"
echo Codigo de saida da restauracao: %RC%
if not "%RC%"=="0" goto restauracao_falhou
echo.
echo Verificando o banco restaurado, conforme o passo 1 da imagem...
"%FB_BIN%\gfix.exe" -validate -full "%DB_NOVO%"
set "RC=%errorlevel%"
echo Codigo de saida da verificacao: %RC%
echo Banco restaurado: "%DB_NOVO%"
echo Examine as mensagens e teste os dados e o sistema antes de usar a nova base.
echo O menu continua apontando para o banco original.
goto pausa

:restauracao_falhou
echo A restauracao falhou. Nao utilize a base parcial que possa ter sido criada.
echo Corrija a causa indicada e use outro destino na proxima tentativa.
goto pausa

:backup_ausente
echo ERRO: arquivo de backup nao encontrado.
goto pausa

:destino_existente
echo ERRO: o destino ja existe. Escolha outro nome; nada foi sobrescrito.
goto pausa

:rollback
echo Esta operacao desfaz transacoes em limbo, nao todas as transacoes abertas.
choice /c SN /n /m "Executar rollback de TODAS as transacoes em limbo [S/N]: "
if errorlevel 2 goto menu
"%FB_BIN%\gfix.exe" -rollback all "%DB%"
goto resultado

:shutdown
echo O shutdown forcado interrompe o acesso ao banco imediatamente.
echo Use a opcao 7 para coloca-lo online novamente.
choice /c SN /n /m "Executar shutdown agora [S/N]: "
if errorlevel 2 goto menu
"%FB_BIN%\gfix.exe" -shut -force 0 "%DB%"
goto resultado

:online
"%FB_BIN%\gfix.exe" -online "%DB%"
goto resultado

:sweep
"%FB_BIN%\gfix.exe" -sweep "%DB%"
goto resultado

:resultado
set "RC=%errorlevel%"
echo.
echo Codigo de saida: %RC%
echo Confira as mensagens acima: codigo zero nao comprova integridade dos dados.
:pausa
echo.
pause
goto menu

:fim
endlocal
exit /b
