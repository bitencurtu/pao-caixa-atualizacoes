using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PaoCaixa;
class SidebarIconsSmoke {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("ICON_PASS "+message);}
 [STAThread]static void Main(){
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  string[] names={"basket","dashboard","package","wallet","receipt","chart","settings","user","lock"};
  string[] pages={"Pedidos","Visão geral","Produtos","Meu caixa","Vendas","Relatórios","Configurações","Meu perfil","Painel Biel"};
  foreach(int size in new[]{16,19,24}){
   var unique=new HashSet<string>();
   foreach(string name in names){
    using(var bitmap=new Bitmap(size,size))
    using(var g=Graphics.FromImage(bitmap)){
     g.Clear(Color.White);
     SidebarIcons.Draw(g,name,new Rectangle(0,0,size,size),Color.FromArgb(32,77,58));
     int drawn=0;var bits=new char[size*size];
     for(int y=0;y<size;y++)for(int x=0;x<size;x++){
      bool on=bitmap.GetPixel(x,y).ToArgb()!=Color.White.ToArgb();
      if(on)drawn++;
      bits[y*size+x]=on?'1':'0';
     }
     Check(drawn>=17,name+" is visible at "+size+"px");
     Check(unique.Add(new string(bits)),name+" is a distinct icon at "+size+"px");
    }
   }
  }
  using(var oldIcon=new Bitmap(24,24))
  using(var g=Graphics.FromImage(oldIcon)){
   g.Clear(Color.White);Icons.Draw(g,"wheat",new Rectangle(0,0,24,24),Color.Black);
   Check(oldIcon.GetPixel(12,12).ToArgb()!=0,"legacy product icons remain available");
  }
  for(int i=0;i<pages.Length;i++)Check(SideNavigation.IconFor(pages[i])==names[i],"menu mapping: "+pages[i]);
  using(var navigation=new SideNavigation()){
   navigation.Size=new Size(232,860);navigation.ShopName="Padaria Teste";navigation.UserName="Operador";navigation.Role="Administrador";navigation.Reset(pages);
   foreach(bool dark in new[]{false,true}){
    Ui.Dark=dark;
    navigation.ThemeChanged();
    using(var bmp=new Bitmap(232,860)){
     navigation.DrawToBitmap(bmp,new Rectangle(0,0,232,860));
     Check(bmp.GetPixel(35,180).A==255,"sidebar paints "+(dark?"dark":"light")+" theme");
    }
   }
  }
  Console.WriteLine("SIDEBAR_ICONS_SMOKE_DONE");
 }
}
