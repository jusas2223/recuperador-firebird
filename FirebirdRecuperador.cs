using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FirebirdRecuperador
{
	internal sealed class Janela : Form
	{
		private readonly TextBox banco = new TextBox();

		private readonly TextBox saida = new TextBox();

		private readonly ComboBox ferramenta = new ComboBox();

		private readonly TextBox usuario = new TextBox();

		private readonly TextBox senha = new TextBox();

		private readonly TextBox log = new TextBox();

		private readonly Label etapa = new Label();

		private readonly ProgressBar progresso = new ProgressBar();

		private readonly Button iniciar = new Button();

		private readonly Button cancelar = new Button();

		private readonly Button abrir = new Button();

		private volatile bool cancelado;

		private volatile bool emExecucao;

		private Process processo;

		private string pastaResultado;

		// Observa as etapas concluídas e produz apenas o relatório em linguagem simples.
		private RelatorioLegivel relatorioLegivel;

		private readonly object trava = new object();

		[STAThread]
		private static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Janela(args));
		}

		private Janela(string[] args)
		{
			Text = "Recuperador de bancos Firebird";
			base.ClientSize = new Size(850, 690);
			MinimumSize = new Size(760, 640);
			base.StartPosition = FormStartPosition.CenterScreen;
			Font = new Font("Segoe UI", 9f);
			AllowDrop = true;
			base.DragEnter += delegate(object s, DragEventArgs e)
			{
				if (e.Data.GetDataPresent(DataFormats.FileDrop))
				{
					e.Effect = DragDropEffects.Copy;
				}
			};
			base.DragDrop += delegate(object s, DragEventArgs e)
			{
				string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (array.Length > 0)
				{
					banco.Text = array[0];
				}
			};
			Label value = new Label
			{
				Text = "Recuperar banco Firebird",
				Font = new Font("Segoe UI", 16f, FontStyle.Bold),
				Location = new Point(20, 15),
				AutoSize = true
			};
			base.Controls.Add(value);
			Label value2 = new Label
			{
				Text = "O arquivo original é preservado. A ferramenta cria uma pasta com cópia, backup, banco restaurado e logs.",
				Location = new Point(22, 53),
				Size = new Size(800, 35)
			};
			base.Controls.Add(value2);
			AdicionarLinha("Banco .FDB/.GDB", banco, 92, "Escolher...", EscolherBanco);
			AdicionarLinha("Pasta para resultados", saida, 164, "Escolher...", EscolherSaida);
			Label value3 = new Label
			{
				Text = "Pasta bin do Firebird (use a versão compatível com o banco)",
				Location = new Point(22, 237),
				AutoSize = true
			};
			base.Controls.Add(value3);
			ferramenta.Location = new Point(22, 258);
			ferramenta.Size = new Size(695, 27);
			ferramenta.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			ferramenta.DropDownStyle = ComboBoxStyle.DropDown;
			base.Controls.Add(ferramenta);
			Button button = new Button
			{
				Text = "Escolher...",
				Location = new Point(730, 257),
				Size = new Size(100, 28),
				Anchor = (AnchorStyles.Top | AnchorStyles.Right)
			};
			button.Click += delegate
			{
				EscolherBin();
			};
			base.Controls.Add(button);
			Label value4 = new Label
			{
				Text = "Usuário",
				Location = new Point(22, 301),
				AutoSize = true
			};
			Label value5 = new Label
			{
				Text = "Senha",
				Location = new Point(298, 301),
				AutoSize = true
			};
			base.Controls.Add(value4);
			base.Controls.Add(value5);
			usuario.Location = new Point(22, 322);
			usuario.Size = new Size(250, 25);
			usuario.Text = "SYSDBA";
			senha.Location = new Point(298, 322);
			senha.Size = new Size(260, 25);
			senha.UseSystemPasswordChar = true;
			base.Controls.Add(usuario);
			base.Controls.Add(senha);
			iniciar.Text = "Iniciar recuperação";
			iniciar.Location = new Point(22, 367);
			iniciar.Size = new Size(185, 36);
			iniciar.Click += delegate
			{
				Iniciar();
			};
			cancelar.Text = "Cancelar";
			cancelar.Location = new Point(217, 367);
			cancelar.Size = new Size(105, 36);
			cancelar.Enabled = false;
			cancelar.Click += delegate
			{
				Cancelar();
			};
			abrir.Text = "Abrir pasta de resultados";
			abrir.Location = new Point(332, 367);
			abrir.Size = new Size(190, 36);
			abrir.Enabled = false;
			abrir.Click += delegate
			{
				if (Directory.Exists(pastaResultado))
				{
					Process.Start("explorer.exe", "\"" + pastaResultado + "\"");
				}
			};
			base.Controls.Add(iniciar);
			base.Controls.Add(cancelar);
			base.Controls.Add(abrir);
			etapa.Text = "Pronto para iniciar";
			etapa.Location = new Point(22, 418);
			etapa.Size = new Size(800, 24);
			etapa.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			base.Controls.Add(etapa);
			progresso.Location = new Point(22, 446);
			progresso.Size = new Size(808, 18);
			progresso.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			progresso.Maximum = 100;
			base.Controls.Add(progresso);
			log.Location = new Point(22, 478);
			log.Size = new Size(808, 190);
			log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			log.Multiline = true;
			log.ScrollBars = ScrollBars.Both;
			log.ReadOnly = true;
			log.WordWrap = false;
			log.Font = new Font("Consolas", 9f);
			base.Controls.Add(log);
			base.AcceptButton = iniciar;
			DescobrirFerramentas();
			if (args.Length > 0 && File.Exists(args[0]))
			{
				banco.Text = args[0];
			}
			base.FormClosing += delegate(object s, FormClosingEventArgs e)
			{
				if (emExecucao)
				{
					e.Cancel = true;
					MessageBox.Show("Cancele a operação e aguarde o encerramento antes de fechar.");
				}
			};
		}

		private void AdicionarLinha(string rotulo, TextBox campo, int y, string botao, EventHandler clique)
		{
			base.Controls.Add(new Label
			{
				Text = rotulo,
				Location = new Point(22, y),
				AutoSize = true
			});
			campo.Location = new Point(22, y + 21);
			campo.Size = new Size(695, 25);
			campo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			base.Controls.Add(campo);
			Button button = new Button();
			button.Text = botao;
			button.Location = new Point(730, y + 20);
			button.Size = new Size(100, 28);
			button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			Button button2 = button;
			button2.Click += clique;
			base.Controls.Add(button2);
		}

		private void EscolherBanco(object s, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Filter = "Bancos Firebird (*.fdb;*.gdb)|*.fdb;*.gdb|Todos os arquivos|*.*";
				openFileDialog.CheckFileExists = true;
				if (openFileDialog.ShowDialog(this) == DialogResult.OK)
				{
					banco.Text = openFileDialog.FileName;
					if (saida.Text.Trim().Length == 0)
					{
						saida.Text = Path.GetDirectoryName(openFileDialog.FileName);
					}
				}
			}
		}

		private void EscolherSaida(object s, EventArgs e)
		{
			using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
			{
				folderBrowserDialog.Description = "Escolha a pasta que receberá uma subpasta RECUPERACAO_...";
				if (Directory.Exists(saida.Text))
				{
					folderBrowserDialog.SelectedPath = saida.Text;
				}
				if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
				{
					saida.Text = folderBrowserDialog.SelectedPath;
				}
			}
		}

		private void EscolherBin()
		{
			using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
			{
				folderBrowserDialog.Description = "Selecione a pasta bin que contém gbak.exe e gfix.exe";
				if (Directory.Exists(ferramenta.Text))
				{
					folderBrowserDialog.SelectedPath = ferramenta.Text;
				}
				if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
				{
					ferramenta.Text = folderBrowserDialog.SelectedPath;
				}
			}
		}

		private void DescobrirFerramentas()
		{
			List<string> list = new List<string>();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			list.Add(Path.Combine(folderPath, "Firebird"));
			if (folderPath2 != folderPath)
			{
				list.Add(Path.Combine(folderPath2, "Firebird"));
			}
			foreach (string item in list)
			{
				if (!Directory.Exists(item))
				{
					continue;
				}
				string[] directories = Directory.GetDirectories(item);
				foreach (string path in directories)
				{
					string text = Path.Combine(path, "bin");
					if (File.Exists(Path.Combine(text, "gbak.exe")) && File.Exists(Path.Combine(text, "gfix.exe")))
					{
						ferramenta.Items.Add(text);
					}
				}
			}
			if (ferramenta.Items.Count == 1)
			{
				ferramenta.SelectedIndex = 0;
			}
		}

		private void Iniciar()
		{
			Config c = new Config
			{
				Banco = banco.Text.Trim().Trim('"'),
				PastaSaida = saida.Text.Trim().Trim('"'),
				Bin = ferramenta.Text.Trim().Trim('"'),
				Usuario = usuario.Text.Trim(),
				Senha = senha.Text
			};
			if (!File.Exists(c.Banco))
			{
				Erro("Selecione um arquivo de banco existente.");
				return;
			}
			string extension = Path.GetExtension(c.Banco);
			if (!extension.Equals(".fdb", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".gdb", StringComparison.OrdinalIgnoreCase))
			{
				Erro("Selecione um arquivo .FDB ou .GDB.");
			}
			else if (c.Banco.StartsWith("\\\\"))
			{
				Erro("Selecione um banco local no servidor Firebird, não um caminho de rede.");
			}
			else if (!Directory.Exists(c.PastaSaida))
			{
				Erro("Selecione uma pasta de resultados existente.");
			}
			else if (!File.Exists(Path.Combine(c.Bin, "gbak.exe")) || !File.Exists(Path.Combine(c.Bin, "gfix.exe")))
			{
				Erro("A pasta bin deve conter gbak.exe e gfix.exe.");
			}
			else if (c.Usuario.Length == 0 || c.Senha.Length == 0)
			{
				Erro("Informe o usuário e a senha do Firebird.");
			}
			else if (MessageBox.Show(this, "Este fluxo atende bancos locais de um único arquivo. Feche o sistema e desconecte os usuários antes da cópia física.\n\nO arquivo original será preservado. Continuar?", "Antes de começar", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				senha.Clear();
				cancelado = false;
				relatorioLegivel = null;
				emExecucao = true;
				pastaResultado = null;
				log.Clear();
				iniciar.Enabled = false;
				cancelar.Enabled = true;
				abrir.Enabled = false;
				progresso.Value = 0;
				Thread thread = new Thread((ThreadStart)delegate
				{
					Executar(c);
				});
				thread.IsBackground = true;
				thread.Start();
			}
		}

		private void Erro(string mensagem)
		{
			MessageBox.Show(this, mensagem, "Verifique os dados", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}

		private void Cancelar()
		{
			cancelado = true;
			Adicionar("Cancelamento solicitado. Aguardando encerramento da etapa atual...");
			lock (trava)
			{
				try
				{
					if (processo != null && !processo.HasExited)
					{
						processo.Kill();
					}
				}
				catch
				{
				}
			}
			cancelar.Enabled = false;
		}

		private void Atualizar(string titulo, int valor)
		{
			if (!base.IsDisposed)
			{
				BeginInvoke((Action)delegate
				{
					etapa.Text = titulo;
					progresso.Value = Math.Min(100, Math.Max(0, valor));
				});
			}
		}

		private void Adicionar(string texto)
		{
			if (base.IsDisposed)
			{
				return;
			}
			BeginInvoke((Action)delegate
			{
				log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + texto + Environment.NewLine);
				if (log.TextLength > 160000)
				{
					log.Text = log.Text.Substring(log.TextLength - 120000);
				}
			});
		}

		private void VerificarCancelamento()
		{
			if (cancelado)
			{
				throw new OperationCanceledException();
			}
		}

		private void Copiar(string origem, string destino, int inicio, int fim)
		{
			VerificarCancelamento();
			using (FileStream fileStream = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				using (FileStream fileStream2 = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					long length = fileStream.Length;
					long num = 0L;
					byte[] array = new byte[4194304];
					Stopwatch stopwatch = Stopwatch.StartNew();
					int num2;
					while ((num2 = fileStream.Read(array, 0, array.Length)) > 0)
					{
						VerificarCancelamento();
						fileStream2.Write(array, 0, num2);
						num += num2;
						if (stopwatch.ElapsedMilliseconds > 500)
						{
							int valor = ((length == 0) ? fim : (inicio + (int)((fim - inicio) * num / length)));
							Atualizar("Copiando: " + num / 1048576 + " / " + length / 1048576 + " MB", valor);
							stopwatch.Restart();
						}
					}
					fileStream2.Flush();
					if (num != length)
					{
						throw new IOException("A cópia terminou com tamanho diferente do original.");
					}
				}
			}
			Atualizar("Cópia concluída", fim);
		}

		private int Rodar(string exe, string argumentos, string arquivoLog, Config c, int pct, out bool houveSaida)
		{
			VerificarCancelamento();
			string fileName = Path.GetFileName(arquivoLog);
			Adicionar("Executando " + fileName);
			Atualizar("Executando " + fileName, pct);
			bool saida = false;
			int linhas = 0;
			object logLock = new object();
			StreamWriter arquivo = new StreamWriter(arquivoLog, false, Encoding.UTF8);
			try
			{
				using (Process process = new Process())
				{
					process.StartInfo.FileName = exe;
					process.StartInfo.Arguments = argumentos;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.StartInfo.EnvironmentVariables["ISC_USER"] = c.Usuario;
					process.StartInfo.EnvironmentVariables["ISC_PASSWORD"] = c.Senha;
					DataReceivedEventHandler value = delegate(object sender, DataReceivedEventArgs e)
					{
						if (e.Data != null)
						{
							lock (logLock)
							{
								arquivo.WriteLine(e.Data);
								arquivo.Flush();
								saida = true;
								linhas++;
							}
							if (linhas <= 8 || linhas % 25 == 0 || e.Data.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								Adicionar(e.Data);
							}
						}
					};
					process.OutputDataReceived += value;
					process.ErrorDataReceived += value;
					lock (trava)
					{
						processo = process;
						process.Start();
					}
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					process.WaitForExit();
					process.WaitForExit();
					lock (trava)
					{
						processo = null;
					}
					VerificarCancelamento();
					houveSaida = saida;
					Adicionar(fileName + ": código " + process.ExitCode);
					if (relatorioLegivel != null)
					{
						relatorioLegivel.RegistrarFerramenta(fileName, process.ExitCode, saida);
					}
					return process.ExitCode;
				}
			}
			finally
			{
				if (arquivo != null)
				{
					((IDisposable)arquivo).Dispose();
				}
			}
		}

		private static string Q(string x)
		{
			return "\"" + x.Replace("\"", "\\\"") + "\"";
		}

		private bool ConfirmarReparo(string logFalha)
		{
			DialogResult resultado = DialogResult.No;
			Invoke((Action)delegate
			{
				resultado = MessageBox.Show(this, "O backup normal falhou. Consulte " + logFalha + ".\n\nSe o erro for senha, permissão, versão incompatível ou serviço parado, corrija a causa e execute novamente.\n\nDeseja tentar reparar uma CÓPIA? O reparo e o backup de recuperação podem perder registros danificados e transações em limbo. O original e a cópia anterior ao reparo permanecem preservados.", "Autorizar reparo da cópia", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			});
			return resultado == DialogResult.Yes;
		}

		private void Relatorio(string texto)
		{
			if (pastaResultado == null || !Directory.Exists(pastaResultado))
			{
				return;
			}
			try
			{
				File.WriteAllText(Path.Combine(pastaResultado, "LEIA_O_RESULTADO.txt"), texto, Encoding.UTF8);
			}
			catch (Exception ex)
			{
				Adicionar("Não foi possível gravar o relatório: " + ex.Message);
			}
		}

		private void AguardarServicoSeParado()
		{
			try
			{
				bool flag = false;
				bool flag2 = false;
				ServiceController[] services = ServiceController.GetServices();
				foreach (ServiceController serviceController in services)
				{
					if (serviceController.ServiceName.IndexOf("Firebird", StringComparison.OrdinalIgnoreCase) < 0 || serviceController.ServiceName.IndexOf("Guardian", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						serviceController.Dispose();
						continue;
					}
					flag = true;
					if (serviceController.Status == ServiceControllerStatus.Running)
					{
						flag2 = true;
					}
					serviceController.Dispose();
				}
				if (!flag || flag2)
				{
					return;
				}
				Invoke((Action)delegate
				{
					MessageBox.Show(this, "A cópia foi criada, mas não há serviço Firebird ativo. Inicie o serviço agora e clique OK para continuar com o backup.", "Iniciar Firebird", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				});
				VerificarCancelamento();
				ServiceController[] services2 = ServiceController.GetServices();
				foreach (ServiceController serviceController2 in services2)
				{
					if (serviceController2.ServiceName.IndexOf("Firebird", StringComparison.OrdinalIgnoreCase) >= 0 && serviceController2.ServiceName.IndexOf("Guardian", StringComparison.OrdinalIgnoreCase) < 0 && serviceController2.Status == ServiceControllerStatus.Running)
					{
						serviceController2.Dispose();
						return;
					}
					serviceController2.Dispose();
				}
				throw new Exception("O serviço Firebird continua parado. Inicie-o e execute novamente.");
			}
			catch (Win32Exception)
			{
				Adicionar("Não foi possível consultar os serviços Firebird; o backup tentará continuar.");
			}
		}

		private void Executar(Config c)
		{
			string text = "";
			try
			{
				c.Banco = Path.GetFullPath(c.Banco);
				c.PastaSaida = Path.GetFullPath(c.PastaSaida);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(c.Banco);
				string text2 = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
				pastaResultado = Path.Combine(c.PastaSaida, "RECUPERACAO_" + text2);
				Directory.CreateDirectory(pastaResultado);
				relatorioLegivel = new RelatorioLegivel(pastaResultado, c.Banco);
				BeginInvoke((Action)delegate
				{
					abrir.Enabled = true;
				});
				Adicionar("Resultados: " + pastaResultado);
				Relatorio("RECUPERAÇÃO EM ANDAMENTO\r\nBanco de origem: " + c.Banco + "\r\nO arquivo original não deve ser substituído.\r\n");
				long length = new FileInfo(c.Banco).Length;
				try
				{
					DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(c.PastaSaida));
					if (driveInfo.AvailableFreeSpace < length * 3)
					{
						Adicionar("AVISO: espaço livre inferior a 3 vezes o tamanho do banco. Verifique o disco antes de prosseguir.");
					}
				}
				catch
				{
				}
				string text3 = Path.Combine(pastaResultado, fileNameWithoutExtension + "_ORIGINAL_COPIA" + Path.GetExtension(c.Banco));
				string text4 = text3;
				string text5 = Path.Combine(pastaResultado, fileNameWithoutExtension + "_BACKUP.fbk");
				string novo = Path.Combine(pastaResultado, fileNameWithoutExtension + "_RECUPERADO.fdb");
				string exe = Path.Combine(c.Bin, "gbak.exe");
				string exe2 = Path.Combine(c.Bin, "gfix.exe");
				Atualizar("1/5 Copiando banco original", 2);
				Adicionar("A cópia requer acesso exclusivo ao arquivo. Feche os usuários e o sistema.");
				Copiar(c.Banco, text3, 2, 25);
				relatorioLegivel.RegistrarCopiaOriginal(text3);
				Adicionar("Cópia preservada: " + text3);
				AguardarServicoSeParado();
				bool houveSaida;
				if (Rodar(exe2, "-validate -full -no_update " + Q(text4), Path.Combine(pastaResultado, "01_diagnostico.log"), c, 30, out houveSaida) != 0 || houveSaida)
				{
					Adicionar("O diagnóstico gerou mensagens. Confira 01_diagnostico.log.");
				}
				int num = Rodar(exe, "-backup -g -verbose " + Q(text4) + " " + Q(text5), Path.Combine(pastaResultado, "02_backup_normal.log"), c, 45, out houveSaida);
				bool flag = false;
				if (num != 0 || !File.Exists(text5) || new FileInfo(text5).Length == 0)
				{
					if (!ConfirmarReparo(Path.Combine(pastaResultado, "02_backup_normal.log")))
					{
						throw new Exception("Backup normal falhou e o reparo não foi autorizado.");
					}
					VerificarCancelamento();
					flag = true;
					relatorioLegivel.RegistrarReparoAutorizado();
					text4 = Path.Combine(pastaResultado, fileNameWithoutExtension + "_TRABALHO_REPARO" + Path.GetExtension(c.Banco));
					Atualizar("3/5 Preservando cópia antes do reparo", 50);
					Copiar(text3, text4, 50, 60);
					relatorioLegivel.RegistrarCopiaReparo(text4);
					if (Rodar(exe2, "-mend -ignore " + Q(text4), Path.Combine(pastaResultado, "03_reparo.log"), c, 65, out houveSaida) != 0)
					{
						throw new Exception("O reparo falhou. Consulte 03_reparo.log.");
					}
					text5 = Path.Combine(pastaResultado, fileNameWithoutExtension + "_BACKUP_APOS_REPARO.fbk");
					if (Rodar(exe, "-backup -g -ignore -limbo -verbose " + Q(text4) + " " + Q(text5), Path.Combine(pastaResultado, "04_backup_apos_reparo.log"), c, 75, out houveSaida) != 0 || !File.Exists(text5) || new FileInfo(text5).Length == 0)
					{
						throw new Exception("O backup após o reparo falhou. Consulte 04_backup_apos_reparo.log.");
					}
				}
				if (Rodar(exe, "-create -verbose " + Q(text5) + " " + Q(novo), Path.Combine(pastaResultado, "05_restauracao.log"), c, 85, out houveSaida) != 0 || !File.Exists(novo) || new FileInfo(novo).Length == 0)
				{
					throw new Exception("A restauração falhou. Não use o banco recuperado parcial. Consulte 05_restauracao.log.");
				}
				num = Rodar(exe2, "-validate -full -no_update " + Q(novo), Path.Combine(pastaResultado, "06_verificacao_final.log"), c, 95, out houveSaida);
				bool alertaValidacao = num != 0 || houveSaida;
				text = "BANCO RESTAURADO\r\n\r\nOrigem (preservada): " + c.Banco + "\r\nCópia do banco antigo: " + text3 + "\r\nBackup Firebird: " + text5 + "\r\nBanco novo: " + novo + "\r\n\r\n";
				if (alertaValidacao)
				{
					text += "ATENÇÃO: a validação final gerou mensagens. Leia 06_verificacao_final.log antes de usar o banco.\r\n";
				}
				if (flag)
				{
					text += "ATENÇÃO: houve reparo com possível perda de registros ou transações em limbo.\r\n";
				}
				text += "Teste cadastros, movimentações, relatórios e dados recentes no sistema antes de usar o banco restaurado. A integridade dos dados não é garantida apenas por esta ferramenta.\r\n";
				Relatorio(text);
				relatorioLegivel.DefinirResultado("Banco restaurado", null);
				Atualizar(alertaValidacao ? "Restaurado com alerta na verificação" : "Concluído: confira o banco novo e os logs", 100);
				Adicionar("Restauração terminada. Banco novo: " + novo);
				BeginInvoke((Action)delegate
				{
					MessageBox.Show(this, "Banco restaurado em:\n" + novo + "\n\nLeia LEIA_O_RESULTADO.txt e teste o sistema antes de usar a base.", alertaValidacao ? "Restauração com alerta" : "Recuperação concluída", MessageBoxButtons.OK, alertaValidacao ? MessageBoxIcon.Exclamation : MessageBoxIcon.Asterisk);
				});
			}
			catch (OperationCanceledException)
			{
				text = "OPERAÇÃO CANCELADA\r\n\r\nNão use arquivos parciais. O banco original foi preservado.\r\nResultados: " + pastaResultado;
				Relatorio(text);
				if (relatorioLegivel != null)
				{
					relatorioLegivel.DefinirResultado("Operação cancelada", null);
				}
				Adicionar("Operação cancelada. Arquivos parciais permanecem para análise.");
				Atualizar("Cancelado", 0);
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				text = "RECUPERAÇÃO INTERROMPIDA\r\n\r\n" + ex3.Message + "\r\n\r\nNão use arquivos parciais. Consulte os logs. O banco original foi preservado.\r\nResultados: " + pastaResultado;
				Relatorio(text);
				if (relatorioLegivel != null)
				{
					relatorioLegivel.DefinirResultado("Recuperação interrompida", ex3.Message);
				}
				Adicionar("ERRO: " + ex3.Message);
				Atualizar("Falha: consulte o resultado e os logs", 0);
				BeginInvoke((Action)delegate
				{
					MessageBox.Show(this, ex3.Message + "\n\nConfira a pasta de resultados e os logs.", "Recuperação interrompida", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				});
			}
			finally
			{
				if (relatorioLegivel != null)
				{
					try
					{
						relatorioLegivel.Salvar();
						Adicionar("Relatório em linguagem simples: " + Path.Combine(pastaResultado, "LEIA_O_RESULTADO.txt"));
					}
					catch (Exception ex4)
					{
						Adicionar("Não foi possível completar o relatório: " + ex4.Message);
					}
				}
				c.Senha = null;
				BeginInvoke((Action)delegate
				{
					iniciar.Enabled = true;
					cancelar.Enabled = false;
					emExecucao = false;
				});
			}
		}
	}
}

namespace FirebirdRecuperador
{
	internal sealed class Config
	{
		public string Banco;

		public string PastaSaida;

		public string Bin;

		public string Usuario;

		public string Senha;
	}
}
