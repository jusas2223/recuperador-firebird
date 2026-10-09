@echo off
setlocal
set "COMPILADOR=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%COMPILADOR%" set "COMPILADOR=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%COMPILADOR%" (
  echo Compilador C# do .NET Framework nao encontrado.
  exit /b 1
)
"%COMPILADOR%" /nologo /codepage:65001 /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll /out:"%~dp0RecuperadorFirebird.exe" "%~dp0FirebirdRecuperador.cs" "%~dp0RelatorioLegivel.cs"
if errorlevel 1 exit /b 1
echo Executavel criado: "%~dp0RecuperadorFirebird.exe"
endlocal
