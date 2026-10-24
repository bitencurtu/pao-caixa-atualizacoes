using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
namespace PaoCaixa {
 public class Product { public string id,name,code,category,unit,icon; public long price,cost,stock,minStock; public bool active; }
 public class Line { public string productId; public long quantity; }
 public class Item:Line { public string name,code,unit; public long price,cost,total; }
 public class Payment { public string method; public long amount; }
 public class Sale { public string id,sessionId,at,operatorName,userId,note,status,cancelledAt,cancelReason; public string @operator {get{return operatorName;}set{operatorName=value;}} public long number,subtotal,discount,total,cashReceived,change; public List<Item> items=new List<Item>(); public List<Payment> payments=new List<Payment>(); }
 public class Register { public string id,openedAt,closedAt,operatorName,userId,status; public string @operator {get{return operatorName;}set{operatorName=value;}} public long opening,difference; public Dictionary<string,long> counted,expected; }
 public class CashMove { public string id,sessionId,at,type,reason; public long amount; }
 public class StockMove { public string id,productId,at,reason,saleId; public long quantity; }
 public class Settings {public string name,@operator,footer;}
 public class State { public int schemaVersion=1; public long revision,nextSale; public Settings settings; public List<Product> products=new List<Product>(); public List<Sale> sales=new List<Sale>(); public List<Register> sessions=new List<Register>(); public List<CashMove> cashMoves=new List<CashMove>(); public List<StockMove> stockMoves=new List<StockMove>(); }
 public class Draft { public List<Line> cart=new List<Line>(); public string discount="",note=""; }
 public class User { public string id,email,name,role,salt,password_hash,created_at; public long is_owner,active,credential_version; public bool Owner {get{return is_owner==1;}} }
 public class ControlState {public long blocked; public string reason,changed_at;}
 public class Totals {public Dictionary<string,long> payments=D.Methods.ToDictionary(x=>x,x=>0L); public long revenue,supplied,withdrawn,drawer; public int sales;}
 public class Cart {public List<Item> items=new List<Item>(); public long subtotal,discount,total;}
 public static class J {
  public static string Write(object x) {return Serializer().Serialize(x);}
  public static T Read<T>(string s) {return Serializer().Deserialize<T>(s);}
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};}
 }
 public static class D {
  public static readonly CultureInfo BR=CultureInfo.GetCultureInfo("pt-BR");
  public static readonly string[] Methods={"dinheiro","pix","debito","credito"};
  public static readonly string[] MethodNames={"Dinheiro","Pix","Débito","Crédito"};
  public static string Id(){return Guid.NewGuid().ToString();}
  public static string Now(){return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture);}
  public static string Money(long n){return (n/100m).ToString("C2",BR);}
  public static string Qty(long n,string unit){return (n/1000m).ToString(unit=="kg"?"0.###":"0",BR);}
  public static long Parse(string raw,int scale,bool signed=false){
   decimal n; string t=(raw??"").Trim().Replace(',','.');
   if(!System.Text.RegularExpressions.Regex.IsMatch(t,signed?@"^-?\d+(\.\d+)?$":@"^\d+(\.\d+)?$") || !decimal.TryParse(t,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n)) throw new Exception("Informe um número válido.");
   long v=(long)Math.Round(n*scale,0,MidpointRounding.AwayFromZero); Int(Math.Abs(v),"Valor"); return v;
  }
  public static void Int(long n,string label,long min=0){if(n<min||n>1000000000)throw new Exception(label+": informe um valor válido.");}
  public static string Text(string s,string label,int max=200){if(string.IsNullOrWhiteSpace(s)||s.Trim().Length>max)throw new Exception("Preencha "+label+" (até "+max+" caracteres).");return s.Trim();}
  public static long Rounded(long price,long q){return (long)Math.Round(price*q/1000m,0,MidpointRounding.AwayFromZero);}
  public static Cart Calculate(List<Product> products,List<Line> lines,long discount){
   Int(discount,"Desconto");var c=new Cart{discount=discount};var seen=new HashSet<string>();
   foreach(var l in lines){if(!seen.Add(l.productId))throw new Exception("Produto duplicado.");var p=products.Find(x=>x.id==l.productId&&x.active); if(p==null)throw new Exception("Produto indisponível.");Int(l.quantity,"Quantidade",1);if(p.unit=="un"&&l.quantity%1000!=0)throw new Exception("Informe uma quantidade inteira para "+p.name+".");if(l.quantity>p.stock)throw new Exception("Estoque insuficiente de "+p.name+".");
    c.items.Add(new Item{productId=p.id,name=p.name,code=p.code,unit=p.unit,quantity=l.quantity,price=p.price,cost=p.cost,total=Rounded(p.price,l.quantity)});
   }
   c.subtotal=c.items.Sum(x=>x.total);Int(c.subtotal,"Total");if(discount>c.subtotal)throw new Exception("O desconto supera o subtotal.");c.total=c.subtotal-discount;return c;
  }
  public static Totals Count(Register r,IEnumerable<Sale> sales,IEnumerable<CashMove> moves){var t=new Totals();foreach(var s in sales.Where(x=>x.sessionId==r.id&&x.status=="paid")){t.sales++;t.revenue+=s.total;foreach(var p in s.payments)t.payments[p.method]+=p.amount;}foreach(var m in moves.Where(x=>x.sessionId==r.id)){if(m.type=="supply")t.supplied+=m.amount;else t.withdrawn+=m.amount;}t.drawer=r.opening+t.payments["dinheiro"]+t.supplied-t.withdrawn;return t;}
  static void Date(string s){DateTime d;if(!DateTime.TryParse(s,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out d))throw new Exception("Data inválida no banco.");}
  public static State Validate(State s){
   if(s==null||s.schemaVersion!=1||s.products==null||s.sales==null||s.sessions==null||s.cashMoves==null||s.stockMoves==null||s.settings==null||s.settings.name==null||s.settings.@operator==null||s.settings.footer==null)throw new Exception("Formato de backup não reconhecido.");Int(s.revision,"Revisão");Int(s.nextSale,"Próxima venda",1);
   var ids=new HashSet<string>();var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in s.products){if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id)||string.IsNullOrEmpty(p.code)||!codes.Add(p.code)||p.name==null||p.category==null||p.icon==null||(p.unit!="un"&&p.unit!="kg"))throw new Exception("Produto inválido no banco.");Int(p.price,"Preço",1);Int(p.cost,"Custo");Int(p.stock,"Estoque");Int(p.minStock,"Estoque mínimo");if(p.unit=="un"&&(p.stock%1000!=0||p.minStock%1000!=0))throw new Exception("Estoque por unidade inválido.");}
   var sessions=new HashSet<string>();int open=0;
   foreach(var r in s.sessions){if(string.IsNullOrEmpty(r.id)||!sessions.Add(r.id)||r.operatorName==null||(r.status!="open"&&r.status!="closed"))throw new Exception("Caixa inválido.");Date(r.openedAt);Int(r.opening,"Fundo");if(r.status=="open")open++;else{Date(r.closedAt);if(r.counted==null||r.expected==null)throw new Exception("Fechamento inválido.");foreach(var m in Methods){if(!r.counted.ContainsKey(m)||!r.expected.ContainsKey(m))throw new Exception("Fechamento inválido.");Int(r.counted[m],"Contagem");Int(r.expected[m],"Contagem");}if(r.difference!=r.counted["dinheiro"]-r.expected["dinheiro"])throw new Exception("Diferença inválida.");}}
   if(open>1)throw new Exception("Há mais de um caixa aberto.");var salesIds=new HashSet<string>();var numbers=new HashSet<long>();
   foreach(var v in s.sales){if(string.IsNullOrEmpty(v.id)||!salesIds.Add(v.id)||!sessions.Contains(v.sessionId)||v.items==null||v.items.Count==0||v.payments==null||v.operatorName==null||v.note==null||(v.status!="paid"&&v.status!="cancelled"))throw new Exception("Venda inválida.");Int(v.number,"Venda",1);if(!numbers.Add(v.number)||v.number>=s.nextSale)throw new Exception("Numeração inválida.");Date(v.at);foreach(var n in new[]{v.subtotal,v.discount,v.total,v.cashReceived,v.change})Int(n,"Venda");
    foreach(var i in v.items){if(!ids.Contains(i.productId)||i.name==null||i.code==null||(i.unit!="un"&&i.unit!="kg"))throw new Exception("Item inválido.");Int(i.quantity,"Quantidade",1);Int(i.price,"Preço",1);Int(i.cost,"Custo");Int(i.total,"Total");if(i.unit=="un"&&i.quantity%1000!=0||i.total!=Rounded(i.price,i.quantity))throw new Exception("Total do item inválido.");}
    var pm=new HashSet<string>();foreach(var p in v.payments){if(!Methods.Contains(p.method)||!pm.Add(p.method))throw new Exception("Pagamento inválido.");Int(p.amount,"Pagamento",1);}long cash=v.payments.Where(p=>p.method=="dinheiro").Sum(p=>p.amount);
    if(v.subtotal!=v.items.Sum(i=>i.total)||v.total!=v.subtotal-v.discount||v.total<=0||v.total!=v.payments.Sum(p=>p.amount)||v.cashReceived<cash||(cash==0&&v.cashReceived!=0)||v.change!=v.cashReceived-cash)throw new Exception("Totais inconsistentes.");if(v.status=="cancelled"){Date(v.cancelledAt);Text(v.cancelReason,"o motivo do cancelamento");}
   }
   var mi=new HashSet<string>();foreach(var m in s.cashMoves){if(string.IsNullOrEmpty(m.id)||!mi.Add(m.id)||!sessions.Contains(m.sessionId)||(m.type!="supply"&&m.type!="withdrawal")||m.reason==null)throw new Exception("Movimentação inválida.");Int(m.amount,"Movimentação",1);Date(m.at);}
   mi.Clear();foreach(var m in s.stockMoves){if(string.IsNullOrEmpty(m.id)||!mi.Add(m.id)||!ids.Contains(m.productId)||Math.Abs(m.quantity)>1000000000||m.reason==null||(!string.IsNullOrEmpty(m.saleId)&&!salesIds.Contains(m.saleId)))throw new Exception("Estoque inválido.");Date(m.at);}
   var ts=s.sessions.ToDictionary(r=>r.id,r=>new Totals{drawer=r.opening});foreach(var v in s.sales.Where(v=>v.status=="paid"))foreach(var p in v.payments)ts[v.sessionId].payments[p.method]+=p.amount;foreach(var m in s.cashMoves)ts[m.sessionId].drawer+=m.type=="supply"?m.amount:-m.amount;
   foreach(var r in s.sessions){var t=ts[r.id];t.drawer+=t.payments["dinheiro"];if(t.drawer<0)throw new Exception("Saldo de caixa negativo.");if(r.status=="closed"&&Methods.Any(m=>r.expected[m]!=(m=="dinheiro"?t.drawer:t.payments[m])))throw new Exception("Fechamento inconsistente.");}return s;
  }
 }
}
