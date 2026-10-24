using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
[assembly:AssemblyTitle("Pão & Caixa")]
[assembly:AssemblyProduct("Pão & Caixa")]
[assembly:AssemblyVersion("2.0.7.0")]
[assembly:AssemblyFileVersion("2.0.7.0")]
namespace PaoCaixa {
 static class Program {
  [STAThread] static void Main(){bool first;using(var mutex=new Mutex(true,"Local\\PaoCaixaNative",out first)){if(!first){MessageBox.Show("Pão & Caixa já está aberto neste usuário do Windows.","Pão & Caixa");return;}try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PaoCaixa");Directory.CreateDirectory(directory);Application.Run(new MainForm(directory));}catch(Exception e){MessageBox.Show("Não foi possível iniciar o programa.\n"+e.Message+"\nO banco original não foi apagado.","Pão & Caixa",MessageBoxButtons.OK,MessageBoxIcon.Error);}}}
 }
}
