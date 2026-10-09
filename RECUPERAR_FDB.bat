@echo off
setlocal
set "RECUPERAR_FDB_BAT=%~f0"
set "RECUPERAR_FDB_ENTRADA=%~1"
title Recuperar banco FDB
echo ============================================================
echo RECUPERAR FDB - acompanhamento na tela
echo ============================================================
echo Iniciando. As perguntas serao feitas nesta janela.
echo Voce tambem pode arrastar um arquivo .FDB sobre este .BAT.
echo.
powershell.exe -NoProfile -STA -Command "$text = [IO.File]::ReadAllText($env:RECUPERAR_FDB_BAT); $code = ($text -split '(?m)^# POWERSHELL_START\r?$', 2)[1]; & ([scriptblock]::Create($code))"
if errorlevel 1 echo O processo terminou com erro. Confira a mensagem acima.
echo.
pause
endlocal
exit /b
# POWERSHELL_START
# Banco Firebird local, composto por um unico arquivo.
# O original e aberto somente para leitura; as ferramentas usam a copia.
# Referencia: https://www.firebirdsql.org/file/documentation/html/en/firebirddocs/gfix/firebird-gfix.html
$ErrorActionPreference = 'Stop'
function Mensagem([string]$texto, [bool]$erro = $false) {
    if ($erro) { Write-Host $texto -ForegroundColor Red }
    else { Write-Host $texto -ForegroundColor Cyan }
}
function Confirmar([string]$texto) {
    Write-Host ''
    Write-Host $texto -ForegroundColor Yellow
    return ((Read-Host 'Digite S para continuar, ou Enter para cancelar').Trim() -ieq 'S')
}
function Executar([string]$programa, [string[]]$parametros, [string]$arquivoLog) {
    Write-Host ('Etapa: ' + [IO.Path]::GetFileNameWithoutExtension($arquivoLog)) -ForegroundColor Cyan
    Write-Host 'Aguarde. Nao feche esta janela.'
    Write-Host ('Log desta etapa: ' + $arquivoLog)
    $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = $null
    & $programa @parametros 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $arquivoLog | Out-Host
    $codigoEtapa = $global:LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($null -eq $codigoEtapa) { throw ('Nao foi possivel iniciar a ferramenta: ' + $programa) }
    Write-Host ('Etapa encerrada. Codigo de saida: ' + $codigoEtapa)
    return $codigoEtapa
}
$pasta = $null
try {
    Write-Host '[1/5] Arquivo do banco' -ForegroundColor Cyan
    $original = $env:RECUPERAR_FDB_ENTRADA
    if ([string]::IsNullOrWhiteSpace($original)) {
        Write-Host 'Cole o caminho completo do .FDB e pressione Enter.'
        Write-Host 'Exemplo: C:\Sistema\DADOS.FDB'
        $original = Read-Host 'Caminho do banco'
    }
    $original = $original.Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($original)) { Write-Host 'Cancelado: nenhum banco informado.'; exit 0 }
    if (-not (Test-Path -LiteralPath $original -PathType Leaf)) { throw ('Arquivo nao encontrado: ' + $original) }
    $original = (Get-Item -LiteralPath $original).FullName
    if ([IO.Path]::GetExtension($original) -ine '.fdb') { throw 'Informe um arquivo .FDB.' }
    Write-Host ('Banco selecionado: ' + $original)
    if ($original.StartsWith('\\')) { throw 'Execute no servidor do banco e selecione um caminho local, por exemplo C:\Dados\BANCO.FDB.' }

    Write-Host '[2/5] Localizando as ferramentas Firebird...' -ForegroundColor Cyan
    $gbak = $null
    foreach ($candidato in @("$env:ProgramFiles\Firebird\Firebird_2_5\bin\gbak.exe", "${env:ProgramFiles(x86)}\Firebird\Firebird_2_5\bin\gbak.exe")) {
        if (Test-Path -LiteralPath $candidato -PathType Leaf) { $gbak = $candidato; break }
    }
    if (-not $gbak) {
        Mensagem 'Firebird 2.5 nao encontrado no caminho padrao. Cole abaixo o caminho completo do gbak.exe da versao usada pelo seu sistema.'
        Write-Host 'Exemplo: C:\Program Files\Firebird\Firebird_2_5\bin\gbak.exe'
        $gbak = (Read-Host 'Caminho do gbak.exe').Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($gbak)) { Write-Host 'Cancelado.'; exit 0 }
    }
    if (-not (Test-Path -LiteralPath $gbak -PathType Leaf)) { throw ('Ferramenta nao encontrada: ' + $gbak) }
    if ([IO.Path]::GetFileName($gbak) -ine 'gbak.exe') { throw 'Selecione a ferramenta gbak.exe.' }
    $gfix = Join-Path ([IO.Path]::GetDirectoryName($gbak)) 'gfix.exe'
    if (-not (Test-Path -LiteralPath $gfix -PathType Leaf)) { throw 'Nao foi encontrado gfix.exe na mesma pasta do gbak.exe.' }
    Write-Host ('Ferramentas: ' + [IO.Path]::GetDirectoryName($gbak))
    $seguir = Confirmar "Feche o sistema que utiliza o banco e desconecte os usuarios.`r`n`r`nO programa fara uma copia e trabalhara nela. O original sera preservado.`r`n`r`nEste procedimento atende bancos compostos por UM unico arquivo .FDB.`r`n`r`nO banco tem apenas um arquivo e o sistema esta fechado?"
    if (-not $seguir) { Write-Host 'Cancelado. Nenhum reparo foi executado.'; exit 0 }
    Write-Host '[3/5] Acesso ao Firebird' -ForegroundColor Cyan
    $usuario = Read-Host 'Usuario do Firebird [Enter = SYSDBA]'
    if ([string]::IsNullOrWhiteSpace($usuario)) { $usuario = 'SYSDBA' }
    Write-Host 'Digite a senha do Firebird e pressione Enter. A senha ficara oculta.'
    $senhaSegura = Read-Host 'Senha' -AsSecureString
    $credencial = New-Object System.Management.Automation.PSCredential($usuario, $senhaSegura)
    $env:ISC_USER = $usuario
    $env:ISC_PASSWORD = $credencial.GetNetworkCredential().Password
    if ([string]::IsNullOrEmpty($env:ISC_PASSWORD)) { throw 'A senha do Firebird nao foi informada.' }

    $nome = [IO.Path]::GetFileNameWithoutExtension($original)
    $sufixo = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
    $pasta = Join-Path ([IO.Path]::GetDirectoryName($original)) ('RECUPERACAO_' + $sufixo)
    $null = New-Item -ItemType Directory -Path $pasta
    $copia = Join-Path $pasta ($nome + '_COPIA_TRABALHO.fdb')
    $backup = Join-Path $pasta ($nome + '_BACKUP.fbk')
    $recuperado = Join-Path $pasta ($nome + '_RECUPERADO.fdb')
    Write-Host '[4/5] Criando uma copia de trabalho...' -ForegroundColor Cyan
    Write-Host ('Pasta dos resultados: ' + $pasta)
    $leitura = $null
    $gravacao = $null
    try {
        # Acesso exclusivo impede copiar um arquivo que esteja aberto em outro processo.
        $leitura = [IO.File]::Open($original, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $gravacao = [IO.File]::Open($copia, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $buffer = New-Object byte[] (4MB)
        $copiado = 0L
        $relogio = [Diagnostics.Stopwatch]::StartNew()
        while (($lidos = $leitura.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $gravacao.Write($buffer, 0, $lidos)
            $copiado += $lidos
            if ($relogio.Elapsed.TotalSeconds -ge 2) {
                Write-Host ('Copiando: {0:N0} de {1:N0} MB' -f ($copiado / 1MB), ($leitura.Length / 1MB))
                $relogio.Restart()
            }
        }
        $gravacao.Flush()
        Write-Host 'Copia concluida.' -ForegroundColor Green
    } catch {
        throw ('Nao foi possivel copiar o banco com acesso exclusivo. Feche o sistema e as conexoes ao banco. Se necessario, pare o servico Firebird durante a copia. Verifique tambem permissoes e espaco em disco. Nenhum reparo foi iniciado. Detalhe: ' + $_.Exception.Message)
    } finally {
        if ($null -ne $gravacao) { $gravacao.Dispose() }
        if ($null -ne $leitura) { $leitura.Dispose() }
    }
    $servicos = @(Get-Service -Name '*Firebird*' -ErrorAction SilentlyContinue)
    if ($servicos.Count -gt 0 -and @($servicos | Where-Object { $_.Status -eq 'Running' }).Count -eq 0) {
        Mensagem 'A copia foi criada. Inicie agora o servico Firebird. O programa nao inicia ou para servicos automaticamente.'
        $null = Read-Host 'Depois de iniciar o servico, pressione Enter'
    }

    Write-Host '[5/5] Backup, recuperacao e verificacao. Acompanhe abaixo.' -ForegroundColor Cyan
    $codigo = Executar $gbak @('-backup', '-g', '-verbose', $copia, $backup) (Join-Path $pasta '01_backup_normal.log')
    $usouReparo = $false
    if ($codigo -ne 0) {
        $tentar = Confirmar "O backup falhou. Consulte:`r`n$pasta\01_backup_normal.log`r`n`r`nSe o erro for senha, servico parado ou permissao, pressione Enter para cancelar e corrija a causa.`r`n`r`nTentar reparar a COPIA? O reparo pode descartar registros corrompidos e dados de transacoes em limbo. O original sera preservado."
        if (-not $tentar) { throw 'Recuperacao interrompida. Consulte o log da tentativa de backup.' }
        $usouReparo = $true
        $codigo = Executar $gfix @('-validate', '-full', '-ignore', $copia) (Join-Path $pasta '02_validacao_copia.log')
        # A validacao pode apontar corrupcao. O reparo foi autorizado na janela anterior.
        $codigo = Executar $gfix @('-mend', '-ignore', $copia) (Join-Path $pasta '03_reparo_copia.log')
        if ($codigo -ne 0) { throw 'O reparo retornou erro. Consulte 03_reparo_copia.log. O processo foi interrompido.' }
        $backup = Join-Path $pasta ($nome + '_BACKUP_APOS_REPARO.fbk')
        $codigo = Executar $gbak @('-backup', '-g', '-ignore', '-limbo', '-verbose', $copia, $backup) (Join-Path $pasta '04_backup_apos_reparo.log')
        if ($codigo -ne 0) { throw 'O backup apos o reparo falhou. Consulte 04_backup_apos_reparo.log. Este caso precisa de analise especifica.' }
    }
    if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw 'O backup nao foi criado. A restauracao nao foi iniciada.' }
    $codigo = Executar $gbak @('-create', '-verbose', $backup, $recuperado) (Join-Path $pasta '05_restauracao.log')
    if ($codigo -ne 0) { throw 'A restauracao falhou. Nao use o arquivo _RECUPERADO.fdb parcial. Consulte 05_restauracao.log.' }
    if (-not (Test-Path -LiteralPath $recuperado -PathType Leaf)) { throw 'O arquivo recuperado nao foi encontrado.' }
    $logFinal = Join-Path $pasta '06_verificacao_final.log'
    $codigo = Executar $gfix @('-validate', '-full', '-no_update', $recuperado) $logFinal
    $textoValidacao = [string](Get-Content -LiteralPath $logFinal -Raw -ErrorAction SilentlyContinue)
    $resultado = "Novo banco criado:`r`n$recuperado`r`n`r`nTeste os dados e o funcionamento do sistema antes de usar este banco. O original nao foi substituido."
    if ($codigo -ne 0 -or -not [string]::IsNullOrWhiteSpace($textoValidacao)) {
        $resultado += "`r`n`r`nATENCAO: a verificacao final gerou mensagens. Consulte 06_verificacao_final.log antes de usar o arquivo."
    }
    if ($usouReparo) { $resultado += "`r`n`r`nFoi utilizado reparo com possivel descarte de dados. A recuperacao completa nao e garantida." }
    $resultado | Set-Content -LiteralPath (Join-Path $pasta 'LEIA_O_RESULTADO.txt') -Encoding UTF8
    Mensagem $resultado
    Start-Process -FilePath 'explorer.exe' -ArgumentList ('/select,"' + $recuperado + '"')
} catch {
    $erro = $_.Exception.Message
    if ($pasta) { $erro += "`r`n`r`nArquivos e logs desta tentativa:`r`n$pasta" }
    Mensagem $erro $true
    exit 1
} finally {
    Remove-Item Env:ISC_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:ISC_USER -ErrorAction SilentlyContinue
}
