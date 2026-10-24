using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PaoCaixa;

class ResponsiveTestForm:MainForm {
 public ResponsiveTestForm(string path):base(path){}
 protected override void OnShown(EventArgs e){} // Fixture local: nao inicia conexoes remotas.
}
class ResponsiveScreenTests {
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool ok,string name){if(!ok)throw new Exception("RESPONSIVE_FAIL "+name);Console.WriteLine("RESPONSIVE_PASS "+name);}
 static object Field(object obj,string name){return obj.GetType().BaseType.GetField(name,F).GetValue(obj);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().BaseType.GetMethod(name,F).Invoke(obj,args);}
 static void TestGeometry(){
  foreach(var r in new[]{new Rectangle(0,0,800,600),new Rectangle(0,0,1024,728),new Rectangle(0,0,1366,728),new Rectangle(0,0,1920,1040),new Rectangle(1920,0,1280,700)}){
   var start=ResponsiveLayout.InitialClientSize(r);var min=ResponsiveLayout.MinimumWindowSize(r);
   Check(start.Width<=r.Width&&start.Height<=r.Height&&min.Width<=r.Width&&min.Height<=r.Height,"initial dimensions fit "+r.Width+"x"+r.Height);
  }
  Check(ResponsiveLayout.SidebarWidth(800)<ResponsiveLayout.SidebarWidth(1280),"sidebar shrinks on narrow monitor");
  Check(ResponsiveLayout.ContentWidth(1900)==1160,"wide content is not stretched indefinitely");
  Check(ResponsiveLayout.ContentWidth(490)<=490,"narrow content never exceeds available width");
  Check(StatsRow.HeightFor(1000,4)==147&&StatsRow.HeightFor(570,4)==310&&StatsRow.HeightFor(390,4)==636,"stat cards change from four to two to one per row");
  foreach(var w in new[]{390,590,1010}){
   using(var stats=new StatsRow(new StatCard("Um","chart"),new StatCard("Dois","chart"),new StatCard("Três","chart"),new StatCard("Quatro","chart"))){
    stats.SetBounds(0,0,w,147);stats.PerformLayout();
    Check(stats.Height==StatsRow.HeightFor(w,4),"stat card container height for width "+w);
    foreach(Control c in stats.Controls)Check(c.Left>=0&&c.Top>=0&&c.Right<=stats.Width&&c.Bottom<=stats.Height,"stat cards stay inside row "+w);
   }
  }
  using(var bar=new TopBar()){
   foreach(var w in new[]{480,560,710,980}){
    bar.Size=new Size(w,70);bar.PerformLayout();
    using(var bmp=new Bitmap(w,70))bar.DrawToBitmap(bmp,new Rectangle(0,0,w,70));
    var badge=(Rectangle)typeof(TopBar).GetField("cashHit",F).GetValue(bar);
    Check(badge.X>=0&&badge.Right+5<=bar.Theme.Left,"topbar cash indicator does not collide with theme at "+w);
    Check(bar.Theme.Right<=bar.Width&&bar.Theme.Left>=0,"theme switch fits "+w);
   }
  }
  using(var side=new SideNavigation()){
   side.Reset(new[]{"Pedidos","Visão geral","Produtos","Meu caixa","Vendas","Relatórios","Configurações","Meu perfil","Painel Biel"});
   foreach(var h in new[]{470,500,600,640,800}){
    side.Size=new Size(194,h);
    using(var bmp=new Bitmap(194,h))side.DrawToBitmap(bmp,new Rectangle(0,0,194,h));
    var hits=(IList)typeof(SideNavigation).GetField("hit",F).GetValue(side);
    Check(hits.Count==9,"all menu items visible "+h);
    foreach(Rectangle rect in hits)Check(rect.Top>=0&&rect.Bottom<=h&&rect.Width>0,"menu item inside navigation "+h);
    var logout=(Rectangle)typeof(SideNavigation).GetField("logoutHit",F).GetValue(side);
    Check(logout.Bottom<=h&&logout.Top>=0,"logout fits menu height "+h);
   }
  }
 }
 static void TestForm(string dataDir){
  using(var form=new ResponsiveTestForm(dataDir)){
   form.Show();
   var nav=(SideNavigation)Field(form,"nav");
   var shell=(Panel)Field(form,"shell");
   var body=(Panel)Field(form,"body");
   nav.Visible=true;
   typeof(MainForm).GetField("preparingPage",F).SetValue(form,true);
   Call(form,"Root","Produtos");
   var content=(Panel)Call(form,"Content",true);
   foreach(var size in new[]{new Size(800,550),new Size(1024,690),new Size(1366,720),new Size(1920,1000)}){
    form.ClientSize=size;form.PerformLayout();body.PerformLayout();
    var expectedSide=ResponsiveLayout.SidebarWidth(size.Width);
    Check(nav.Width==expectedSide,"sidebar width at "+size.Width);
    var inset=ResponsiveLayout.BodyPadding(shell.ClientSize.Width);
    Check(body.Padding.Left==inset,"content padding at "+size.Width);
    var usableW=body.ClientSize.Width-body.Padding.Horizontal;
    Check(content.Width==Math.Min(1160,Math.Max(1,usableW)),"grid pages max width at "+size.Width);
    Check(content.Left>=body.Padding.Left&&content.Right<=body.ClientSize.Width-body.Padding.Right,"content is centered and fits "+size.Width);
    Check(content.Top>=body.Padding.Top,"content below heading "+size.Width);
   }
   form.Close();
  }
 }
 [STAThread]static void Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  TestGeometry();TestForm(args[0]);Console.WriteLine("RESPONSIVE_SCREEN_TESTS_DONE");
 }
}
