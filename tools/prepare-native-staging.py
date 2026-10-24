import io, pathlib, zipfile

source=pathlib.Path("PaoCaixa-2.0.9-Source.zip")
out=pathlib.Path("PaoCaixa-2.0.10-Source.zip")
with zipfile.ZipFile(source) as zipin:
    members=[(info,zipin.read(info.filename)) for info in zipin.infolist()]

file="pao-caixa-nativo/src/MainForm.cs"
matches=[n for n,(info,_) in enumerate(members) if info.filename==file]
assert len(matches)==1, "MainForm ausente"
index=matches[0]
info,original=members[index]
bom=original.startswith(b"\xef\xbb\xbf")
s=original.decode("utf-8-sig")

def replace_once(old,new):
    global s
    assert s.count(old)==1, ("ancora ausente/duplicada",old[:150],s.count(old))
    s=s.replace(old,new,1)

replace_once(
 "bool busy,initializing=true,closingForUpdate,updateFlowRunning;int pageLoadSerial;OwnerView ownerViewReady;string page;",
 "bool busy,initializing=true,closingForUpdate,updateFlowRunning,preparingPage;int pageLoadSerial;OwnerView ownerViewReady;string page;")

replace_once(
 "var h=new PageHeading(SideNavigation.Title(caption),description,caption==\"Pedidos\"?\"TUDO PRONTO PARA ATENDER\":null);body.Controls.Add(h);body.ResumeLayout();RefreshShell();",
 "var h=new PageHeading(SideNavigation.Title(caption),description,caption==\"Pedidos\"?\"TUDO PRONTO PARA ATENDER\":null);body.Controls.Add(h);body.ResumeLayout();if(!preparingPage)RefreshShell();")

begin=s.index("  void SwapPage(Panel previous,Panel next){")
end=s.index("  async void LoadPage(){",begin)
old=s[begin:end]
assert "shell.Controls.Add(next);" in old and "previous.Dispose();" in old and "next.Invalidate(true);" in old
new="""  const int WM_SETREDRAW=0x000B;
  const uint RDW_INVALIDATE=0x0001,RDW_ERASE=0x0004,RDW_ALLCHILDREN=0x0080,RDW_UPDATENOW=0x0100;
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
  static extern bool RedrawWindow(IntPtr hWnd,IntPtr rect,IntPtr region,uint flags);
  // Criar cada HWND nativo enquanto a página está posicionada fora da área visível.
  // Uma árvore destacada não recebe todos os handles de DataGridView, TextBox etc.
  static void CreatePageHandles(Control control){
   if(control.IsDisposed||!control.Visible)return;
   control.CreateControl();
   control.PerformLayout();
   foreach(Control child in control.Controls)CreatePageHandles(child);
  }
  void SwapPage(Panel previous,Panel next){
   // Apenas durante a preparação, não alteramos a página antiga.
   int index=shell.Controls.GetChildIndex(previous);
   next.Dock=DockStyle.None;
   next.SetBounds(shell.ClientSize.Width+64,0,Math.Max(1,previous.Width),Math.Max(1,previous.Height));
   shell.Controls.Add(next);
   try{
    CreatePageHandles(next);
   }catch{
    shell.Controls.Remove(next);
    throw;
   }
   bool suspended=false;
   try{
    // Evita quadros intermediários enquanto reordena os HWND já criados.
    if(shell.IsHandleCreated){SendMessage(shell.Handle,WM_SETREDRAW,IntPtr.Zero,IntPtr.Zero);suspended=true;}
    shell.SuspendLayout();
    try{
     next.Dock=DockStyle.Fill;
     shell.Controls.SetChildIndex(next,index);
     shell.Controls.Remove(previous);
    }finally{shell.ResumeLayout(true);}
   }finally{
    if(suspended&&!shell.IsDisposed){
     SendMessage(shell.Handle,WM_SETREDRAW,new IntPtr(1),IntPtr.Zero);
     RedrawWindow(shell.Handle,IntPtr.Zero,IntPtr.Zero,RDW_INVALIDATE|RDW_ERASE|RDW_ALLCHILDREN|RDW_UPDATENOW);
    }
   }
   previous.Dispose();
  }
"""
s=s[:begin]+new+s[end:]

replace_once(
 '   body=next;\n   try{\n    page=wanted;Root(page);',
 '   body=next;\n   preparingPage=true;\n   try{\n    page=wanted;Root(page);')
replace_once(
 '    Ui.Apply(next);\n    SwapPage(previous,next);',
 '    Ui.Apply(next);\n    SwapPage(previous,next);\n    preparingPage=false;\n    RefreshShell();')
replace_once(
 '   }catch(Exception ex){\n    if(!next.IsDisposed)next.Dispose();',
 '   }catch(Exception ex){\n    preparingPage=false;\n    if(!next.IsDisposed)next.Dispose();')

sbytes=("\ufeff" if bom else "").encode("utf-8")+s.encode("utf-8")
members[index]=(info,sbytes)

version_current=b"2.0.9"
version_new=b"2.0.10"
with zipfile.ZipFile(out,"w") as z:
 for info,data in members:
  if info.filename.endswith((".cs",".json",".nsi",".config",".md",".xml",".mjs",".txt")):
   data=data.replace(version_current,version_new)
  z.writestr(info,data)
with zipfile.ZipFile(out) as z:
 main=z.read(file)
 assert b"CreatePageHandles(next)" in main
 assert b"SendMessage(shell.Handle,WM_SETREDRAW" in main
 assert b"preparingPage=false" in main
 assert b"2.0.10" in z.read("pao-caixa-nativo/version.json")
 assert b'!define VERSION "2.0.10"' in z.read("pao-caixa-nativo/installer.nsi")
 assert b'public const string Version="2.0.10"' in z.read("pao-caixa-nativo/src/Updates.cs")
 print("staged-render ZIP:",out,"bytes",out.stat().st_size)
