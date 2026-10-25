#!/usr/bin/env python3
"""Build bundled, offline bakery product photo atlas from freely licensed Wikimedia Commons photos.
Creates images only during the release build; the POS never makes photo network requests.
Requires Pillow. Third-party author/license/links saved beside the images.
"""
import concurrent.futures, html, io, json, pathlib, re, time, unicodedata, urllib.parse, urllib.request
from PIL import Image, ImageOps, ImageEnhance

ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'resources'
MANIFEST=json.loads((OUT/'photo-manifest.json').read_text(encoding='utf-8'))
BASE='https://commons.wikimedia.org/w/api.php'
HEADERS={'User-Agent':'PaoCaixaPhotoCatalog/2.0 (https://github.com/bitencurtu/pao-caixa-atualizacoes; automated Commons attribution check)'}
W,H=180,126
SKIP=['station','estação','brigadeiro luís','brigadeiro geral','map ','mapa','diagram','logotipo','logo','band','museum','statue','portrait','rail','politician','artwork','painting','recipe book']
def norm(s):
 return ''.join(ch for ch in unicodedata.normalize('NFKD',s.lower()) if not unicodedata.combining(ch))
def get(url):
 req=urllib.request.Request(url,headers=HEADERS)
 for trial in range(4):
  try:
   with urllib.request.urlopen(req,timeout=25) as result:return result.read()
  except Exception:
   if trial==3:raise
   time.sleep(1.6*(trial+1))
def score(title,query):
 title=norm(title.replace('File:',''));query=norm(query)
 tokens=[x for x in re.findall(r'[a-z0-9]+',query) if len(x)>3 and x not in ('with','from','glass','bread','snack','pastry','brazilian','slice','coffee','fried')]
 rank=sum(8 for t in tokens if t in title)
 if all(x in title for x in tokens[:2]):rank+=8
 if any(s in title for s in SKIP):rank-=150
 if title.endswith('.jpg') or title.endswith('.jpeg'):rank+=2
 if len(title)>110:rank-=3
 return rank
def lookup(item):
 query=item['query']
 search_queries=[query]+item.get('fallbacks',[])
 results=[]
 for search_query in search_queries:
  params={'action':'query','format':'json','generator':'search','gsrsearch':search_query,'gsrnamespace':6,'gsrlimit':14,'prop':'imageinfo','iiprop':'url|extmetadata|mime','iiurlwidth':320}
  if item.get('prefer'):
   params.pop('generator')
   params.pop('gsrsearch')
   params.pop('gsrnamespace')
   params.pop('gsrlimit')
   params['titles']='File:'+item['prefer']
  url=BASE+'?'+urllib.parse.urlencode(params)
  data=json.loads(get(url))
  for entry in data.get('query',{}).get('pages',{}).values():
   info=entry.get('imageinfo',[{}])[0]
   thumb=info.get('thumburl') or info.get('url')
   lic=html.unescape(re.sub('<[^>]+>',' ',info.get('extmetadata',{}).get('LicenseShortName',{}).get('value',''))).strip()
   license_name=lic.lower()
   if not any(x in license_name for x in ['cc by','cc0','public domain','pd-','cc-by','free art license']):continue
   if 'nc' in license_name or 'nd' in license_name:continue
   if info.get('mime') not in ('image/jpeg','image/png'):continue
   if not thumb:continue
   title=entry.get('title','')
   points=500 if item.get('prefer') and title.lower()=='file:'+item['prefer'].lower() else score(title,search_query)
   if any(norm(word) in norm(title) for word in item.get('avoid',[])):points-=120
   results.append((points,title,thumb,lic,info.get('descriptionurl',''),info.get('extmetadata',{})))
  if results and (item.get('prefer') or max(row[0] for row in results)>=9):break
  # If a preferred title was missing, the next request uses regular search.
  if item.get('prefer'):
   item=dict(item)
   item.pop('prefer')
   if not search_queries or search_queries[-1]!=query:search_queries.append(query)
 if not results:raise ValueError('No licensed JPEG/PNG candidates for '+item['key']+': '+query)
 results.sort(key=lambda x:x[0],reverse=True)
 failures=[]
 for points,title,url,lic,description,metadata in results[:8]:
  if points<0:continue
  try:
   raw=get(url)
   if len(raw)>3500000:continue
   image=Image.open(io.BytesIO(raw)).convert('RGB')
   if image.width<90 or image.height<90:continue
   image=ImageOps.fit(image,(W,H),Image.Resampling.LANCZOS,centering=(0.5,0.46))
   artist=html.unescape(re.sub('<[^>]+>',' ',metadata.get('Artist',{}).get('value',''))).strip()
   artist=re.sub(r'\s+',' ',artist)[:150]
   print('PHOTO',item['key'],repr(item['name']), '->',title, 'score',points,'license',lic,flush=True)
   return image,{'key':item['key'],'product':item['name'],'file':title,'artist':artist,'license':lic,'source':description}
  except Exception as e:failures.append(str(e))
 raise ValueError('No valid downloadable photo for '+item['key']+': '+repr(failures)[:300])

def main():
 assert len(MANIFEST)==46,'Expected exactly 46 photo entries'
 images=[None]*len(MANIFEST);credits=[None]*len(MANIFEST)
 failures=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
  pending={pool.submit(lookup,item):idx for idx,item in enumerate(MANIFEST)}
  for task in concurrent.futures.as_completed(pending):
   idx=pending[task]
   try:
    image,metadata=task.result()
    images[idx]=image;credits[idx]=metadata
   except Exception as exc:
    print('PHOTO_MISSING',MANIFEST[idx]['key'],str(exc),flush=True)
    failures.append(MANIFEST[idx]['key'])
 if failures:raise ValueError('Unresolved photos: '+', '.join(failures))
 atlas=Image.new('RGB',(8*W,6*H),(245,242,233))
 for idx,img in enumerate(images):atlas.paste(img,((idx%8)*W,(idx//8)*H))
 target=OUT/'catalog-photos.jpg'
 atlas.save(target,'JPEG',quality=84,optimize=True,subsampling=0)
 lines=['# Créditos das fotografias do catálogo','',
  'Estas fotos são exemplos ilustrativos de produtos e não representam mercadorias reais da padaria.',
  'As imagens permanecem sob suas licenças individuais. Para informações de autoria, origem e licença, consulte os links:',
  '']
 for c in credits:lines.append('- **%s:** [%s](%s). Autor: %s. Licença: %s.'%(c['product'],c['file'],c['source'],c['artist'] or 'conforme página de origem',c['license']))
 (OUT/'FOTOS-LICENCAS.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print('ATLAS_READY',target,target.stat().st_size,atlas.size,flush=True)

if __name__=='__main__':main()
