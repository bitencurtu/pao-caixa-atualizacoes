using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace PaoCaixa {
 public class Ranked {public string id,name,unit;public long quantity,value;}
 public class ReportResult {
  public long revenue,cost;public int count,cancelled;public Dictionary<string,long> payments=D.Methods.ToDictionary(x=>x,x=>0L);public List<Ranked> ranking=new List<Ranked>();public Dictionary<string,long> daily=new Dictionary<string,long>();
  public static ReportResult Build(IEnumerable<Sale> sales){var r=new ReportResult();var items=new Dictionary<string,Ranked>();foreach(var v in sales){if(v.status!="paid"){r.cancelled++;continue;}r.count++;r.revenue+=v.total;DateTime at;string day=DateTime.TryParse(v.at,out at)?at.ToLocalTime().ToString("yyyy-MM-dd"):v.at;long amount;r.daily.TryGetValue(day,out amount);r.daily[day]=amount+v.total;foreach(var p in v.payments)r.payments[p.method]+=p.amount;foreach(var i in v.items){r.cost+=D.Rounded(i.cost,i.quantity);string key=i.productId+":"+i.unit;Ranked x;if(!items.TryGetValue(key,out x)){x=new Ranked{id=i.productId,name=i.name,unit=i.unit};items[key]=x;}x.quantity+=i.quantity;x.value+=i.total;}}r.ranking=items.Values.OrderBy(x=>x.unit=="un"?0:1).ThenByDescending(x=>x.quantity).ThenBy(x=>x.name,StringComparer.Create(D.BR,false)).ThenBy(x=>x.id).ToList();return r;}
  static string Cell(string raw){raw=raw??"";if(raw.Length>0&&"=+@-".Contains(raw[0]))raw="'"+raw;return "\""+raw.Replace("\"","\"\"")+"\"";}
  public static string CsvHeader {get{return "Número;Data;Operador;Situação;Subtotal;Desconto;Total;Dinheiro;Pix;Débito;Crédito;Troco;Observação\r\n";}}
  public static string CsvRow(Sale v){var p=D.Methods.Select(m=>(v.payments.Where(x=>x.method==m).Sum(x=>x.amount)/100m).ToString("0.00",D.BR));return string.Join(";",new[]{v.number.ToString(),Cell(v.at),Cell(v.operatorName),Cell(v.status=="paid"?"Concluída":"Cancelada"),(v.subtotal/100m).ToString("0.00",D.BR),(v.discount/100m).ToString("0.00",D.BR),(v.total/100m).ToString("0.00",D.BR)}.Concat(p).Concat(new[]{(v.change/100m).ToString("0.00",D.BR),Cell(v.note)}))+"\r\n";}
 }
}
