
import json
import os

appdata = os.path.join(os.environ['LOCALAPPDATA'], 'XiDeAI')
wisdom_path = os.path.join(appdata, 'GuruWisdom.json')
kb_path = os.path.join(appdata, 'KnowledgeBase.json')

def clean_wisdom():
    if not os.path.exists(wisdom_path):
        print(f"Skipping {wisdom_path} (not found)")
        return
    
    with open(wisdom_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    cleaned = []
    removed_count = 0
    for line in lines:
        if not line.strip(): continue
        try:
            entry = json.loads(line)
            insight = entry.get('Insight', '')
            guru = entry.get('Guru', '')
            
            # Rule 1: No TansuYegen puzzles
            if guru == '@TansuYegen':
                removed_count += 1
                continue
            
            # Rule 2: No "Sistem güncellemesi gerekmiyor"
            if "(Sistem güncellemesi gerekmiyor)" in insight:
                removed_count += 1
                continue
            
            cleaned.append(line)
        except:
            cleaned.append(line)
            
    with open(wisdom_path, 'w', encoding='utf-8') as f:
        f.writelines(cleaned)
    print(f"Cleaned GuruWisdom.json: {removed_count} entries removed.")

def clean_kb():
    if not os.path.exists(kb_path):
        print(f"Skipping {kb_path} (not found)")
        return
    
    with open(kb_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    # Symbols to watch out for in puzzles/noise
    noise_keywords = ["puzzles", "zaka oyunu", "biggest possible number", "two sticks", "move two sticks"]
    
    before_count = len(data)
    # Filter out @TansuYegen (mostly puzzles in this context) and low relevance if they look like noise
    cleaned = [
        item for item in data 
        if item.get('Author') != '@TansuYegen' 
        and not any(k in item.get('Content', '').lower() for k in noise_keywords)
    ]
    
    with open(kb_path, 'w', encoding='utf-8') as f:
        json.dump(cleaned, f, indent=2, ensure_ascii=False)
    
    print(f"Cleaned KnowledgeBase.json: {before_count - len(cleaned)} entries removed.")

if __name__ == "__main__":
    clean_wisdom()
    clean_kb()
