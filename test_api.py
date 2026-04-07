import urllib.request
import json
import ssl
import os

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

api_key = os.environ.get('GEMINI_API_KEY', '').strip()
if not api_key:
    raise RuntimeError('GEMINI_API_KEY env var bulunamadi. Ornek: $env:GEMINI_API_KEY="..."')
url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}'

prompt = '''KİMLİK: Sen deneyimli ve profesyonel bir Baş Ekonomist ve Stratejistsin.

GÖREV: Aşağıdaki finansal haberi, Twitter (X) platformu için 2 ayrı tweet halinde, profesyonel bir dille analiz et. Asla abartılı veya panik yaratacak kelimeler kullanma.

HABER BİLGİLERİ:
- Başlık: Borsa İstanbul kapatılıyor iddialarına yanıt
- Özet: Maliye Bakanı Mehmet Şimşek, Borsa İstanbul'un pazartesi günü işlemlere kapatılacağı yönündeki iddiaların gerçeği yans
- Kaynak: X (Haber)

ÇIKTI FORMATI VE KURALLAR:
1. Analizini tam olarak iki (2) tweet halinde yazmalısın.
2. Tweetleri birbirinden ayırmak için KESİNLİKLE aralarına ||| koymalısın.
3. Birinci tweet, haberin ne olduğunu açıklamalı ve en sona #BIST100 etiketlerini eklemelidir.'''

for temp in [0.3, 0.7]:
    payload = {
        'contents': [{'parts': [{'text': prompt}]}],
        'generationConfig': {
            'maxOutputTokens': 1000,
            'temperature': temp
        }
    }
    req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, context=ctx) as response:
            result = json.loads(response.read().decode('utf-8'))
            print('Temp:', temp, 'Length:', len(result['candidates'][0]['content']['parts'][0]['text']), 'Reason:', result['candidates'][0]['finishReason'])
    except Exception as e:
        print(e)