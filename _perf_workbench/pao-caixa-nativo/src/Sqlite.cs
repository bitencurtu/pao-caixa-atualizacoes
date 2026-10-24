using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace PaoCaixa {
 public sealed class Sqlite:IDisposable {
  IntPtr db; static readonly IntPtr Transient=new IntPtr(-1);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] f,out IntPtr d,int flags,IntPtr vfs);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr d);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr d);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr d,byte[] s,int bytes,out IntPtr p,IntPtr tail);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr s);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr s);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr s,int i,long n);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr s,int i,byte[] n,int len,IntPtr del);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr s,int i);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr s);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr s,int i);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr s,int i);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr s,int i);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr s,int i);
  [DllImport("sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_name(IntPtr s,int i);
  static byte[] Utf(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  static string Ptr(IntPtr p){if(p==IntPtr.Zero)return null;int n=0;while(Marshal.ReadByte(p,n)!=0)n++;var b=new byte[n];Marshal.Copy(p,b,0,n);return Encoding.UTF8.GetString(b);}
  public Sqlite(string file){int rc=sqlite3_open_v2(Utf(file),out db,2|4|0x10000,IntPtr.Zero);if(rc!=0){string e=db==IntPtr.Zero?"Não foi possível abrir o banco.":Ptr(sqlite3_errmsg(db));Dispose();throw new Exception(e);}Exec("PRAGMA busy_timeout=5000");Exec("PRAGMA synchronous=FULL");Exec("PRAGMA foreign_keys=ON");}
  public List<Dictionary<string,object>> Query(string sql,params object[] args){IntPtr stmt=IntPtr.Zero;var rows=new List<Dictionary<string,object>>();try{Check(sqlite3_prepare_v2(db,Utf(sql),-1,out stmt,IntPtr.Zero));for(int i=0;i<args.Length;i++){var a=args[i];if(a==null)Check(sqlite3_bind_null(stmt,i+1));else if(a is string){var b=Encoding.UTF8.GetBytes((string)a);Check(sqlite3_bind_text(stmt,i+1,b,b.Length,Transient));}else Check(sqlite3_bind_int64(stmt,i+1,Convert.ToInt64(a)));}
    int rc;while((rc=sqlite3_step(stmt))==100){var row=new Dictionary<string,object>();for(int c=0;c<sqlite3_column_count(stmt);c++){string name=Ptr(sqlite3_column_name(stmt,c));int type=sqlite3_column_type(stmt,c);row[name]=type==5?null:type==1?(object)sqlite3_column_int64(stmt,c):Ptr(sqlite3_column_text(stmt,c));}rows.Add(row);}if(rc!=101)Check(rc);return rows;
   }finally{if(stmt!=IntPtr.Zero)sqlite3_finalize(stmt);}}
  public void Exec(string sql,params object[] args){Query(sql,args);}
  public object Scalar(string sql,params object[] args){var r=Query(sql,args);return r.Count==0?null:new List<object>(r[0].Values)[0];}
  void Check(int rc){if(rc!=0)throw new Exception("Banco: "+Ptr(sqlite3_errmsg(db)));}
  public void Transaction(Action action){Exec("BEGIN IMMEDIATE");try{action();Exec("COMMIT");}catch{try{Exec("ROLLBACK");}catch{}throw;}}
  public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close_v2(db);db=IntPtr.Zero;}}
 }
}
