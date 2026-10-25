using System;
using System.IO;
using System.Collections.Generic;
using PaoCaixa;
class MigrationAndFeed {
 static void Main(string[] args){using(var db=new Store(args[0]))using(var s=new Sqlite(db.FilePath)){if(Convert.ToInt64(s.Scalar("PRAGMA user_version"))!=2)throw new Exception("Schema antigo não migrado");var bootstrap=J.Read<Dictionary<string,string>>(Store.Resource("owner-bootstrap.json"));if((string)s.Scalar("SELECT password_hash FROM users WHERE is_owner=1")!=bootstrap["password_hash"])throw new Exception("Bootstrap alterado");var draft=J.Read<Draft>((string)s.Scalar("SELECT payload FROM user_drafts WHERE user_id=?",Store.OwnerId));if(draft.note!="Pedido preservado"||draft.cart.Count!=1)throw new Exception("Pedido compartilhado antigo não preservado");Console.WriteLine("PASS migração schema 1, conta proprietária e pedido antigo");}
  var feed=Updates.Parse(File.ReadAllText(args[1]));Updates.Verify(args[2],feed);Console.WriteLine("PASS instalador compilado: metadados, tamanho, PE e SHA-512");var temp=Path.Combine(args[0],"alterado.exe");File.Copy(args[2],temp,true);using(var s=new FileStream(temp,FileMode.Open,FileAccess.ReadWrite)){s.Position=s.Length-1;int x=s.ReadByte();s.Position--;s.WriteByte((byte)(x^1));}bool denied=false;try{Updates.Verify(temp,feed);}catch{denied=true;}if(!denied)throw new Exception("Arquivo alterado aceito");Console.WriteLine("PASS instalador alterado rejeitado");File.Delete(temp);
 }
}
