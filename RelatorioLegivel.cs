using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FirebirdRecuperador
{
    // Somente apresenta fatos observados no fluxo atual. Não executa comandos Firebird.
    internal sealed class RelatorioLegivel
    {
        private sealed class Etapa
        {
            internal int Codigo;
            internal bool TeveMensagens;
        }

        private readonly string pasta;
        private readonly string banco;
        private readonly DateTime inicio;
        private readonly Dictionary<string, Etapa> etapas = new Dictionary<string, Etapa>(StringComparer.OrdinalIgnoreCase);
        private string copiaOriginal;
        private string copiaReparo;
        private bool reparoAutorizado;
        private string resultado;
        private string detalhe;

        internal RelatorioLegivel(string pastaResultado, string bancoOrigem)
        {
            pasta = pastaResultado;
            banco = bancoOrigem;
            inicio = DateTime.Now;
        }

        internal void RegistrarCopiaOriginal(string caminho) { copiaOriginal = caminho; }
        internal void RegistrarCopiaReparo(string caminho) { copiaReparo = caminho; }
        internal void RegistrarReparoAutorizado() { reparoAutorizado = true; }

        internal void RegistrarFerramenta(string nomeLog, int codigo, bool teveMensagens)
        {
            etapas[nomeLog] = new Etapa { Codigo = codigo, TeveMensagens = teveMensagens };
        }

        internal void DefinirResultado(string status, string explicacao)
        {
            resultado = status;
            detalhe = explicacao;
        }

        private Etapa Obter(string nome)
        {
            Etapa e;
            etapas.TryGetValue(nome, out e);
            return e;
        }

        private static bool ArquivoCompleto(string caminho)
        {
            try { return File.Exists(caminho) && new FileInfo(caminho).Length > 0; }
            catch { return false; }
        }

        private void DescreverEtapa(StringBuilder s, string titulo, string nomeLog, string arquivoGerado)
        {
            Etapa e = Obter(nomeLog);
            if (e == null)
            {
                s.AppendLine("- " + titulo + ": não executado ou interrompido antes de receber um resultado.");
                return;
            }
            if (e.Codigo != 0)
            {
                s.AppendLine("- " + titulo + ": falhou (código " + e.Codigo + "). Veja " + nomeLog + ".");
                return;
            }
            if (arquivoGerado != null && !ArquivoCompleto(arquivoGerado))
            {
                s.AppendLine("- " + titulo + ": a ferramenta terminou sem erro, mas o arquivo esperado não foi confirmado. Veja " + nomeLog + ".");
                return;
            }
            s.Append("- " + titulo + ": concluído");
            if (nomeLog.IndexOf("diagnostico", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nomeLog.IndexOf("verificacao", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s.Append(e.TeveMensagens ? ", com mensagens para conferir" : ", sem mensagens da ferramenta");
            }
            s.AppendLine(". Log: " + nomeLog + ".");
        }

        private string LerLogPequeno(string nome)
        {
            string caminho = Path.Combine(pasta, nome);
            if (!File.Exists(caminho)) return "";
            try
            {
                // A saída do gfix é pequena; o limite evita ocupar muita memória em casos anormais.
                using (FileStream f = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader r = new StreamReader(f, Encoding.UTF8, true))
                {
                    char[] buffer = new char[2000000];
                    int lidos = r.ReadBlock(buffer, 0, buffer.Length);
                    return new string(buffer, 0, lidos);
                }
            }
            catch { return ""; }
        }

        private static int? EncontrarContagem(string texto, string nomeErro)
        {
            Match m = Regex.Match(texto, @"(?im)^[ \t]*(?:number[ \t]+of[ \t]+)?" + nomeErro + @"[ \t]*[:=]?[ \t]*(\d+)\b");
            int n;
            if (m.Success && int.TryParse(m.Groups[1].Value, out n)) return n;
            return null;
        }

        private void DescreverPerdas(StringBuilder s)
        {
            s.AppendLine("POSSÍVEL PERDA DE DADOS");
            Etapa reparo = Obter("03_reparo.log");
            if (!reparoAutorizado)
            {
                s.AppendLine("Não houve reparo nesta execução. O programa não compara registros antes e depois; por isso não pode afirmar que todos os dados do banco de origem estavam íntegros.");
            }
            else if (reparo == null)
            {
                s.AppendLine("O reparo foi autorizado, mas não há confirmação de que o comando terminou. Confira os logs e não use arquivos parciais.");
            }
            else
            {
                s.AppendLine("Foi executado gfix -mend -ignore na cópia de trabalho. Esse reparo pode ignorar registros danificados. O arquivo original e a cópia anterior ao reparo não foram submetidos a esse comando.");
                if (Obter("04_backup_apos_reparo.log") != null)
                    s.AppendLine("O backup após o reparo usou -ignore e -limbo; transações em limbo podem ter sido omitidas.");
                string texto = LerLogPequeno("03_reparo.log");
                int? registros = EncontrarContagem(texto, @"record[ \t]+level[ \t]+errors");
                int? indices = EncontrarContagem(texto, @"index[ \t]+page[ \t]+errors");
                int? transacoes = EncontrarContagem(texto, @"transaction[ \t]+page[ \t]+errors");
                if (registros.HasValue)
                    s.AppendLine("O gfix informou " + registros.Value + " erro(s) em nível de registro. Esse número não é uma contagem comprovada de registros perdidos.");
                if (indices.HasValue)
                    s.AppendLine("O gfix informou " + indices.Value + " erro(s) em páginas de índice. Isso não equivale à perda desse número de registros.");
                if (transacoes.HasValue)
                    s.AppendLine("O gfix informou " + transacoes.Value + " erro(s) em páginas de transação.");
                if (!registros.HasValue && !indices.HasValue && !transacoes.HasValue)
                    s.AppendLine("O log do reparo não apresentou uma contagem de erros reconhecível por este relatório.");
                s.AppendLine("As ferramentas não fornecem uma lista confiável dos IDs, nomes ou conteúdos dos registros descartados. A perda exata não pode ser determinada por este relatório.");
            }
            s.AppendLine();
        }

        internal void Salvar()
        {
            string nome = Path.GetFileNameWithoutExtension(banco);
            string fbkNormal = Path.Combine(pasta, nome + "_BACKUP.fbk");
            string fbkReparo = Path.Combine(pasta, nome + "_BACKUP_APOS_REPARO.fbk");
            string restaurado = Path.Combine(pasta, nome + "_RECUPERADO.fdb");
            Etapa validacao = Obter("06_verificacao_final.log");
            StringBuilder s = new StringBuilder();
            s.AppendLine("RELATÓRIO DE RECUPERAÇÃO DO BANCO FIREBIRD");
            s.AppendLine("========================================");
            s.AppendLine("Início: " + inicio.ToString("dd/MM/yyyy HH:mm:ss"));
            s.AppendLine("Fim: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            string status = resultado ?? "Execução sem resultado final registrado";
            if (status == "Banco restaurado" && validacao != null && (validacao.Codigo != 0 || validacao.TeveMensagens))
                status += " — validação final com alertas";
            s.AppendLine("Resultado: " + status);
            if (!String.IsNullOrWhiteSpace(detalhe)) s.AppendLine("Motivo: " + detalhe);
            s.AppendLine();
            s.AppendLine("O QUE O PROGRAMA FEZ");
            s.AppendLine("- Banco de origem: " + banco);
            if (copiaOriginal != null)
                s.AppendLine("- Cópia do banco antigo: concluída em " + copiaOriginal + ".");
            else
                s.AppendLine("- Cópia do banco antigo: não concluída. Nenhum reparo foi aplicado ao banco de origem.");
            DescreverEtapa(s, "Diagnóstico inicial (somente leitura)", "01_diagnostico.log", null);
            DescreverEtapa(s, "Backup normal", "02_backup_normal.log", fbkNormal);
            if (reparoAutorizado)
            {
                s.AppendLine(copiaReparo == null ? "- Cópia para reparo: não concluída." : "- Cópia separada para reparo: concluída em " + copiaReparo + ".");
                DescreverEtapa(s, "Reparo da cópia", "03_reparo.log", null);
                DescreverEtapa(s, "Backup após reparo", "04_backup_apos_reparo.log", fbkReparo);
            }
            else
                s.AppendLine("- Reparo: não executado.");
            DescreverEtapa(s, "Restauração em banco novo", "05_restauracao.log", restaurado);
            DescreverEtapa(s, "Validação final do banco novo", "06_verificacao_final.log", null);
            s.AppendLine();
            DescreverPerdas(s);
            s.AppendLine("ARQUIVOS PARA GUARDAR E CONFERIR");
            if (copiaOriginal != null) s.AppendLine("- Cópia antiga: " + copiaOriginal);
            if (ArquivoCompleto(fbkReparo) && Obter("04_backup_apos_reparo.log") != null && Obter("04_backup_apos_reparo.log").Codigo == 0)
                s.AppendLine("- Backup usado para restaurar: " + fbkReparo);
            else if (ArquivoCompleto(fbkNormal) && Obter("02_backup_normal.log") != null && Obter("02_backup_normal.log").Codigo == 0)
                s.AppendLine("- Backup usado para restaurar: " + fbkNormal);
            if (resultado == "Banco restaurado" && ArquivoCompleto(restaurado))
                s.AppendLine("- Banco novo para testar: " + restaurado);
            else
                s.AppendLine("- Não há confirmação de um banco novo utilizável. Não use arquivos parciais.");
            s.AppendLine("- Logs técnicos completos: arquivos .log nesta mesma pasta.");
            s.AppendLine();
            s.AppendLine("ANTES DE USAR O BANCO NOVO");
            s.AppendLine("Teste no sistema cadastros, movimentações, relatórios e dados recentes. Código zero e validação sem mensagens não garantem a integridade de todos os dados. Não substitua o banco antigo sem conferir o resultado.");
            File.WriteAllText(Path.Combine(pasta, "LEIA_O_RESULTADO.txt"), s.ToString(), new UTF8Encoding(true));
        }
    }
}
