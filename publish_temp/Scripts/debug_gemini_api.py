import json
import requests
import sys
import os

appdata = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'XiDeAI', 'Config.json')
# Load API Key
with open(appdata, 'r', encoding='utf-8') as f:
    config = json.load(f)
api_key = config.get('GeminiApiKey')

prompt = '''KİMLİK: Sen deneyimli ve profesyonel bir Baş Ekonomist ve Stratejistsin.

GÖREV: Aşağıdaki finansal haberi, Twitter (X) platformu için 2 ayrı tweet halinde, profesyonel bir dille analiz et. Asla abartılı veya panik yaratacak kelimeler kullanma.

HABER BİLGİLERİ:
- Başlık: Borsa İstanbul kapatılıyor iddialarına yanıt
- Özet: Maliye Bakanı Mehmet Şimşek, Borsa İstanbul'un pazartesi günü işlemlere kapatılacağı yönündeki iddiaların gerçeği yansıtmadığını bildirdi.
- Kaynak: X (Haber)

ÇIKTI FORMATI VE KURALLAR:
1. Analizini tam olarak iki (2) tweet halinde yazmalısın.
2. Tweetleri birbirinden ayırmak için KESİNLİKLE aralarına ||| koymalısın.
3. Birinci tweet, haberin ne olduğunu açıklamalı ve en sona #BIST100 etiketlerini eklemelidir.
4. İkinci tweet, haberin piyasa etkilerini (fırsat veya riskleri) profesyonelce yorumlamalı ve sonuna 'Y.T.D.' eklemelidir.
5. 'TWEET 1', 'TWEET 2' gibi başlıkları ASLA ÇIKTIYA YAZMA.
6. Çıktın sadec ve sadece tweet metinlerinden ve ayırıcı ||| işaretinden oluşmalıdır.'''

url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}'

payload = {
    'contents': [{'parts': [{'text': prompt}]}],
    'safetySettings': [
        {'category': 'HARM_CATEGORY_HARASSMENT', 'threshold': 'BLOCK_NONE'},
        {'category': 'HARM_CATEGORY_HATE_SPEECH', 'threshold': 'BLOCK_NONE'},
        {'category': 'HARM_CATEGORY_SEXUALLY_EXPLICIT', 'threshold': 'BLOCK_NONE'},
        {'category': 'HARM_CATEGORY_DANGEROUS_CONTENT', 'threshold': 'BLOCK_NONE'}
    ],
    'generationConfig': {
        'maxOutputTokens': 1000,
        'temperature': 0.3
    }
}

try:
    response = requests.post(url, json=payload)
    data = response.json()
    
    if 'error' in data:
        print('API Error:', json.dumps(data['error'], indent=2))
        sys.exit(1)
        
    candidate = data['candidates'][0]
    finish_reason = candidate.get('finishReason', 'UNKNOWN')
    
    print(f'Finish Reason: {finish_reason}')
    
    if finish_reason == 'SAFETY':
        print(f'Safety Ratings: {json.dumps(candidate.get("safetyRatings", []), indent=2)}')
        
    if 'content' in candidate and 'parts' in candidate['content']:
        text = candidate['content']['parts'][0]['text']
        print(f'Generated Text ({len(text)} chars):\n{text}')
        
        # Split logic from C#
        parts = text.split('|||')
        print(f'Found {len(parts)} parts in text')
    else:
        print('No content generated.')
        
except Exception as e:
    print('Script Error:', str(e))
