import io, zipfile, pathlib
src=pathlib.Path("PaoCaixa-2.0.6-Source.zip")
data=src.read_bytes()
needle='void LoadPage(){if(user==null)return;page=nav.SelectedPage;Root(page);'
with zipfile.ZipFile(io.BytesIO(data),'r') as z:
    items={x.filename:(x,z.read(x.filename)) for x in z.infolist()}
name=next(n for n in items if n.endswith('/src/MainForm.cs'))
code=items[name][1].decode('utf-8-sig')
assert code.count(needle)==1, 'Assinatura de LoadPage inesperada'
start=code.index(needle)
end=code.index('\n',start)
old=code[start:end]
assert old.endswith('ApplyTheme();}'), 'Corpo LoadPage desconhecido'
wrapped='''[System.Runtime.InteropServices.DllImport("user32.dll")]
  static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
  void BeginPageTransition(){
   body.SuspendLayout();
   // WM_SETREDRAW evita mostrar a pagina desmontada durante a troca.
   if(body.IsHandleCreated)SendMessage(body.Handle,11,IntPtr.Zero,IntPtr.Zero);
  }
  void EndPageTransition(){
   try{body.ResumeLayout(true);}
   finally{
    if(body.IsHandleCreated)SendMessage(body.Handle,11,new IntPtr(1),IntPtr.Zero);
    body.Invalidate(true);
   }
  }
  '''
replacement=wrapped+'void LoadPage(){if(user==null)return;BeginPageTransition();try{'+old[len('void LoadPage(){if(user==null)return;'):-1]+'}finally{EndPageTransition();}}'
code=code[:start]+code[start:].replace(old,replacement,1)
assert code.count('BeginPageTransition();try{')==1
items[name]=(items[name][0],code.encode('utf-8-sig') if items[name][1].startswith(b'\xef\xbb\xbf') else code.encode('utf-8'))
out=pathlib.Path("PaoCaixa-2.0.7-Source.zip")
# Rewrite all embedded version metadata that the publish workflow validates.
with zipfile.ZipFile(out,'w') as target:
    for n,(info,payload) in items.items():
        if n.endswith(('.cs','.json','.nsi','.config')):
            payload=payload.replace(b'2.0.6',b'2.0.7')
        target.writestr(info,payload)
print('Patch created',out,out.stat().st_size,'bytes')
